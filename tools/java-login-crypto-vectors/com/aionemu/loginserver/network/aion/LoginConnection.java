package com.aionemu.loginserver.network.aion;

import java.nio.ByteBuffer;
import com.aionemu.loginserver.model.Account;

public final class LoginConnection {
	private final Account account;

	public LoginConnection() {
		this(null);
	}

	public LoginConnection(Account account) {
		this.account = account;
	}

	public int encrypt(ByteBuffer buffer) {
		return buffer.remaining();
	}

	public Account getAccount() {
		return account;
	}
}
