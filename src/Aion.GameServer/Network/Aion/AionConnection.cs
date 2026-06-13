using Aion.GameServer.Configs.Main;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Network.Aion;

/// <summary>
/// Java parity: network/aion/AionConnection (-Nemesiss-). Connection between GameServer and Aion client.
/// extends AConnection&lt;AionServerPacket&gt;. java.nio -> shim; AtomicReference -> Interlocked/volatile;
/// synchronized -> lock; currentTimeMillis -> UtcNow.ToUnixTimeMilliseconds; nanoTime -> Stopwatch.
/// Some collaborators (PacketProcessor, AionClientPacketFactory, ExecuteWrapper, RunnableStatsManager,
/// GameServer, *Config, ThreadPoolManager async idiom) are not yet ported and are red-tolerated refs.
/// </summary>
public class AionConnection : AConnection<AionServerPacket>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AionConnection));

    private static readonly PacketProcessor<AionConnection> packetProcessor = new PacketProcessor<AionConnection>(
        NetworkConfig.PACKET_PROCESSOR_MIN_THREADS, NetworkConfig.PACKET_PROCESSOR_MAX_THREADS,
        NetworkConfig.PACKET_PROCESSOR_THREAD_SPAWN_THRESHOLD, NetworkConfig.PACKET_PROCESSOR_THREAD_KILL_THRESHOLD,
        new ExecuteWrapper(ThreadConfig.MAXIMUM_RUNTIME_IN_MILLISEC_WITHOUT_WARNING));

    /// <summary>Possible states of AionConnection.</summary>
    public enum State
    {
        /// <summary>client just connected</summary>
        CONNECTED,
        /// <summary>client is authenticated</summary>
        AUTHED,
        /// <summary>client entered world</summary>
        IN_GAME
    }

    private readonly Queue<AionServerPacket> sendMsgQueue = new Queue<AionServerPacket>();

    private volatile State state;

    private Account account;

    private readonly Crypt crypt = new Crypt();

    private Player activePlayer;

    private long lastClientMessageTime;
    private long lastPingTime;
    private int pingFailCount;

    private const int MAX_CORRUPT_PACKETS_BEFORE_DISCONNECT = 3;
    private int corruptPackets = 0;

    private string macAddress;
    private string hddSerial;

    private ConnectionAliveChecker connectionAliveChecker;

    /// <summary>packet flood filter</summary>
    private Dictionary<int, long> pffRequests;

    public AionConnection(SocketChannel sc, Dispatcher d) : base(sc, d, 8192 * 4, 8192 * 4)
    {
        state = State.CONNECTED;

        string ip = GetIP();
        log.LogDebug("connection from: " + ip);

        lastClientMessageTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        connectionAliveChecker = new ConnectionAliveChecker(this);

        if (PffConfig.PFF_MODE > 0 && PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE != null)
            pffRequests = new Dictionary<int, long>();
    }

    protected override void Initialized()
    {
        SendPacket(new SM_KEY());
    }

    /// <summary>Enable crypt key. Called from the SM_KEY server packet.</summary>
    public int EnableCryptKey()
    {
        return crypt.EnableKey();
    }

    protected override Queue<AionServerPacket> GetSendMsgQueue()
    {
        return sendMsgQueue;
    }

    /// <summary>Called by the Dispatcher with one packet's worth of data to process.</summary>
    protected override bool ProcessData(ByteBuffer data)
    {
        if (!crypt.IsEnabled()) // skip unprocessable packet (client sends crap upon reconnect before Crypt is initialized)
            return true;

        if (!crypt.Decrypt(data))
        {
            if (++corruptPackets >= MAX_CORRUPT_PACKETS_BEFORE_DISCONNECT)
            {
                log.LogWarning("Client packet decryption failed " + corruptPackets + " times, disconnecting " + this);
                return false;
            }
            log.LogDebug("[" + corruptPackets + "/" + MAX_CORRUPT_PACKETS_BEFORE_DISCONNECT + "] Decrypt fail, client packet passed...");
            return true;
        }

        if (data.Remaining() < 5)
        {
            log.LogWarning("Received fake packet from " + this + ", disconnecting");
            return false;
        }

        AionClientPacket pck = AionClientPacketFactory.TryCreatePacket(data, this);

        if (pck != null)
        {
            lastClientMessageTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (pffRequests != null)
            {
                int msBetweenPackets = PffConfig.GetAllowedMillisBetweenPackets(pck);
                if (msBetweenPackets > 0)
                {
                    long last = pffRequests.TryGetValue(pck.GetOpCode(), out long l) ? l : 0;
                    pffRequests[pck.GetOpCode()] = lastClientMessageTime;
                    if (last != 0)
                    {
                        long diff = lastClientMessageTime - last;
                        if (diff < msBetweenPackets)
                        {
                            log.LogWarning(this + " is flooding " + pck.GetType().Name + " (last diff: " + diff + "ms)");
                            if (PffConfig.PFF_MODE == 1) // disconnect
                                return false;
                        }
                    }
                }
            }

            if (pck.Read())
            {
                SendPacketInfo(pck);
                packetProcessor.ExecutePacket(pck);
            }
        }

        return true;
    }

    /// <summary>Called by the Dispatcher repeatedly until it returns false.</summary>
    protected override bool WriteData(ByteBuffer data)
    {
        lock (guard)
        {
            if (sendMsgQueue.Count == 0)
                return false;
            AionServerPacket packet = sendMsgQueue.Dequeue();
            if (packet.GetType() != typeof(SM_MESSAGE))
                SendPacketInfo(packet);
            long begin = Stopwatch.GetTimestamp();
            packet.Write(this, data);
            if (data.Limit() > AionServerPacket.MAX_CLIENT_SUPPORTED_PACKET_SIZE)
                log.LogWarning(packet + " contains " + (data.Limit() - AionServerPacket.MAX_CLIENT_SUPPORTED_PACKET_SIZE) + " more bytes than the game client of " + GetActivePlayer() + " can read");
            return true;
        }
    }

    private bool CanReceivePacketInfoInChat()
    {
        return GetState() == State.IN_GAME && GetAccount().GetMembership() == 10;
    }

    private void SendPacketInfo(BasePacket packet)
    {
        if (CanReceivePacketInfoInChat())
            SendPacket(new SM_MESSAGE(0, null, packet.ToFormattedPacketNameString(), ChatType.BRIGHT_YELLOW));
    }

    internal void SendUnknownClientPacketInfo(int opCode)
    {
        if (CanReceivePacketInfoInChat())
            SendPacket(new SM_MESSAGE(0, null, BasePacket.ToFormattedPacketNameString(3, opCode, "CM_UNK"), ChatType.YELLOW));
    }

    protected override void OnDisconnect()
    {
        connectionAliveChecker.Stop();

        global::Aion.GameServer.Network.LoginServer.LoginServer.GetInstance().OnDisconnect(this);

        string msg = GetAccount() == null ? "" : " " + GetAccount();
        Player player = GetActivePlayer();
        if (player != null)
        {
            msg += " " + player + " (client crash or connection loss)";
            player.GetMoveController().ResetToLastPositionFromClient();
            long millisSinceLastClientPacket = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastClientMessageTime;
            long delayMs = Math.Max(0, 10000 - millisSinceLastClientPacket);
            PlayerLeaveWorldService.LeaveWorldDelayed(player, delayMs);
        }

        if (msg.Length == 0)
            msg = " " + this;

        log.LogInformation("Client disconnected:" + msg);
    }

    public override void OnServerClose()
    {
        Close();
        SafeLogout();
    }

    private void SafeLogout()
    {
        lock (this)
        {
            Player player = GetActivePlayer();
            if (player == null) // player was already saved
                return;
            try
            {
                PlayerLeaveWorldService.LeaveWorld(player);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error saving " + player);
            }
        }
    }

    /// <summary>Encrypt packet.</summary>
    public void Encrypt(ByteBuffer buf)
    {
        crypt.Encrypt(buf);
    }

    public State GetState() => state;

    public void SetState(State state) => this.state = state;

    public Account GetAccount() => account;

    public void SetAccount(Account account)
    {
        this.account = account ?? throw new ArgumentNullException(nameof(account), "Account can't be null");
    }

    /// <summary>Sets the active player and updates the connection state accordingly.</summary>
    public bool SetActivePlayer(Player player)
    {
        if (player == null)
        {
            activePlayer = player;
            SetState(State.AUTHED);
        }
        else if (Interlocked.CompareExchange(ref activePlayer, player, null) == null)
        {
            SetState(State.IN_GAME);
        }
        else
        {
            return false;
        }
        return true;
    }

    public Player GetActivePlayer() => activePlayer;

    public long GetLastClientMessageTime() => lastClientMessageTime;

    public long GetLastPingTime() => lastPingTime;

    public void SetLastPingTime(long time) => lastPingTime = time;

    public int IncreaseAndGetPingFailCount() => ++pingFailCount;

    public void ResetPingFailCount() => pingFailCount = 0;

    public void SetMacAddress(string mac) => this.macAddress = mac;

    public string GetMacAddress() => macAddress;

    public string GetHddSerial() => hddSerial;

    public void SetHddSerial(string hddSerial) => this.hddSerial = hddSerial;

    public override string ToString()
    {
        return "AionConnection [state=" + state + ", account=" + GetAccount() + ", activePlayer=" + activePlayer + ", macAddress=" + macAddress
            + ", getIP()=" + GetIP() + "]";
    }

    private class ConnectionAliveChecker : Runnable
    {
        private readonly AionConnection outer;
        private ScheduledTask task;

        internal ConnectionAliveChecker(AionConnection outer)
        {
            this.outer = outer;
            // Java scheduleAtFixedRate(Runnable, long, long) -> C# async idiom (ThreadPoolManager is async).
            task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
                ct => { Run(); return System.Threading.Tasks.ValueTask.CompletedTask; },
                TimeSpan.FromMilliseconds(CM_PING.CLIENT_PING_INTERVAL),
                TimeSpan.FromMilliseconds(CM_PING.CLIENT_PING_INTERVAL));
        }

        internal void Stop()
        {
            task.Cancel();
        }

        public void Run()
        {
            long millisSinceLastClientPacket = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - outer.lastClientMessageTime;
            if (millisSinceLastClientPacket - 5000 > CM_PING.CLIENT_PING_INTERVAL)
            {
                log.LogInformation("Closing hanged up connection of " + outer + " (last sign of life was " + millisSinceLastClientPacket + "ms ago)");
                outer.Close();
            }
        }
    }
}
