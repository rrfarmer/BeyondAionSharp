package com.aionemu.loginserver.network.gameserver;

import com.aionemu.loginserver.GameServerInfo;

public final class GsConnection {
	private final GameServerInfo gameServerInfo;

	public GsConnection() {
		this(null);
	}

	public GsConnection(GameServerInfo gameServerInfo) {
		this.gameServerInfo = gameServerInfo;
	}

	public GameServerInfo getGameServerInfo() {
		return gameServerInfo;
	}
}
