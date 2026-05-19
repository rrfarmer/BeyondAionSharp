package com.aionemu.loginserver;

import com.aionemu.loginserver.model.Account;
import java.util.HashMap;
import java.util.Map;

public final class GameServerInfo {
	private final byte id;
	private final byte[] ip;
	private final int port;
	private final int currentPlayers;
	private final int maxPlayers;
	private final boolean online;
	private final Map<Integer, Account> accounts = new HashMap<Integer, Account>();

	public GameServerInfo(byte id, byte[] ip, int port, int currentPlayers, int maxPlayers, boolean online) {
		this.id = id;
		this.ip = ip;
		this.port = port;
		this.currentPlayers = currentPlayers;
		this.maxPlayers = maxPlayers;
		this.online = online;
	}

	public byte getId() {
		return id;
	}

	public byte[] getIp() {
		return ip;
	}

	public int getPort() {
		return port;
	}

	public int getCurrentPlayers() {
		return currentPlayers;
	}

	public int getMaxPlayers() {
		return maxPlayers;
	}

	public boolean isOnline() {
		return online;
	}

	public void addAccount(Account account) {
		accounts.put(account.getId(), account);
	}

	public Account getAccountFromGameServer(int accountId) {
		return accounts.get(accountId);
	}
}
