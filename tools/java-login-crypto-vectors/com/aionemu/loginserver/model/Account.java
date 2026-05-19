package com.aionemu.loginserver.model;

public final class Account {
	private final int id;
	private final int lastServer;

	public Account(int id) {
		this(id, -1);
	}

	public Account(int id, int lastServer) {
		this.id = id;
		this.lastServer = lastServer;
	}

	public int getId() {
		return id;
	}

	public int getLastServer() {
		return lastServer;
	}
}
