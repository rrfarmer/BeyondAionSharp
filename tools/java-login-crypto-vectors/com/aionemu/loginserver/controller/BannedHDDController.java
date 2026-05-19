package com.aionemu.loginserver.controller;

import java.sql.Timestamp;
import java.util.LinkedHashMap;
import java.util.Map;

public final class BannedHDDController {
	private static final BannedHDDController INSTANCE = new BannedHDDController();
	private Map<String, Timestamp> map = new LinkedHashMap<String, Timestamp>();

	private BannedHDDController() {
	}

	public static BannedHDDController getInstance() {
		return INSTANCE;
	}

	public Map<String, Timestamp> getMap() {
		return map;
	}

	public static void setMap(Map<String, Timestamp> newMap) {
		INSTANCE.map = newMap;
	}
}
