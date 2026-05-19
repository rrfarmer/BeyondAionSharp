package com.aionemu.loginserver;

public final class GameServerInfo {
	private final byte id;
	private final byte[] ip;
	private final int port;
	private final int currentPlayers;
	private final int maxPlayers;
	private final boolean online;

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
}
