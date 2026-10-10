type StoredAction = {
  requestId: string;
  parameters: string;
};

export type PendingAction = {
  requestId: string;
  receivedResponse: () => void;
};

export function getPendingAction(actionType: string, parameters: unknown): PendingAction {
  const key = `millionnika_pending_action_${actionType}`;
  const serializedParameters = JSON.stringify(parameters);
  const stored = readStoredAction(key);

  if (stored && stored.parameters !== serializedParameters) {
    throw new Error("Сначала повторите незавершённое действие с прежними параметрами.");
  }

  const action = stored ?? { requestId: crypto.randomUUID(), parameters: serializedParameters };
  localStorage.setItem(key, JSON.stringify(action));
  return {
    requestId: action.requestId,
    receivedResponse: () => localStorage.removeItem(key),
  };
}

function readStoredAction(key: string): StoredAction | null {
  try {
    const value = localStorage.getItem(key);
    if (!value) return null;
    const parsed = JSON.parse(value) as Partial<StoredAction>;
    return typeof parsed.requestId === "string" && typeof parsed.parameters === "string"
      ? { requestId: parsed.requestId, parameters: parsed.parameters }
      : null;
  } catch {
    localStorage.removeItem(key);
    return null;
  }
}
