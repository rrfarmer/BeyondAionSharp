package com.aionemu.loginserver.controller;

import java.util.HashMap;
import java.util.Map;

public final class AccountController {
	private static final Map<Integer, Map<Byte, Integer>> countsByAccount = new HashMap<>();

	private AccountController() {
	}

	public static Map<Byte, Integer> getGSCharacterCountsFor(int accountId) {
		Map<Byte, Integer> counts = countsByAccount.get(accountId);
		return counts == null ? new HashMap<Byte, Integer>() : counts;
	}

	public static void setGSCharacterCountsFor(int accountId, Map<Byte, Integer> counts) {
		countsByAccount.put(accountId, counts);
	}
}
