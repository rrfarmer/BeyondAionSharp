package com.aionemu.loginserver.model;

public final class Account {
	private final int id;
	private final int lastServer;
	private final String name;
	private AccountTime accountTime;

	public Account(int id) {
		this(id, -1);
	}

	public Account(int id, int lastServer) {
		this(id, lastServer, "account-" + id, new AccountTime());
	}

	public Account(int id, int lastServer, String name, AccountTime accountTime) {
		this.id = id;
		this.lastServer = lastServer;
		this.name = name;
		this.accountTime = accountTime;
	}

	public int getId() {
		return id;
	}

	public int getLastServer() {
		return lastServer;
	}

	public String getName() {
		return name;
	}

	public AccountTime getAccountTime() {
		return accountTime;
	}
}
