package com.aionemu.loginserver.controller;

import com.aionemu.loginserver.model.base.BannedMacEntry;
import java.util.LinkedHashMap;
import java.util.Map;

public final class BannedMacManager {
	private static final BannedMacManager INSTANCE = new BannedMacManager();
	private Map<String, BannedMacEntry> map = new LinkedHashMap<String, BannedMacEntry>();

	private BannedMacManager() {
	}

	public static BannedMacManager getInstance() {
		return INSTANCE;
	}

	public Map<String, BannedMacEntry> getMap() {
		return map;
	}

	public static void setMap(Map<String, BannedMacEntry> newMap) {
		INSTANCE.map = newMap;
	}
}
