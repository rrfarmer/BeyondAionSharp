package com.aionemu.loginserver;

import java.util.ArrayList;
import java.util.Collection;
import java.util.List;

public final class GameServerTable {
	private static final List<GameServerInfo> gameServers = new ArrayList<>();

	private GameServerTable() {
	}

	public static Collection<GameServerInfo> getGameServers() {
		return gameServers;
	}

	public static void setGameServers(Collection<GameServerInfo> servers) {
		gameServers.clear();
		gameServers.addAll(servers);
	}
}
