"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";

type InventoryItem = {
  itemId: string;
  name: string;
  description: string;
  type: string;
  effectResource: string;
  effectAmount: number;
  quantity: number;
};

type Profile = {
  energy: { current: number; maximum: number };
  nerve: { current: number; maximum: number };
  health: { current: number; maximum: number };
};

type ActionResult = { succeeded: boolean; message: string };

export default function InventoryPage() {
  const router = useRouter();
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [busyItemId, setBusyItemId] = useState<string | null>(null);
  const [message, setMessage] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");

  const loadData = useCallback(async () => {
    const [inventoryResponse, profileResponse] = await Promise.all([
      fetch(`${API_URL}/api/game/inventory`, { credentials: "include" }),
      fetch(`${API_URL}/api/profile`, { credentials: "include" }),
    ]);
    if (inventoryResponse.status === 401 || profileResponse.status === 401) {
      router.replace("/login");
      return;
    }
    if (!inventoryResponse.ok || !profileResponse.ok) throw new Error("Inventory request failed");
    setItems((await inventoryResponse.json()) as InventoryItem[]);
    setProfile((await profileResponse.json()) as Profile);
  }, [router]);

  useEffect(() => {
    void loadData().catch(() => setError("Не удалось загрузить инвентарь."));
  }, [loadData]);

  async function useItem(item: InventoryItem) {
    setError("");
    setMessage(null);
    setBusyItemId(item.itemId);
    const quantity = quantities[item.itemId] ?? 1;

    try {
      const pending = getPendingAction("item_use", { itemId: item.itemId, quantity });
      const response = await fetch(`${API_URL}/api/game/inventory/use`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ requestId: pending.requestId, itemId: item.itemId, quantity }),
      });
      pending.receivedResponse();
      if (response.status === 401) {
        router.replace("/login");
        return;
      }
      if (response.status === 409) {
        setMessage((await response.json()) as ActionResult);
        await loadData();
        return;
      }
      if (!response.ok) {
        setError(await getErrorMessage(response));
        return;
      }
      setMessage((await response.json()) as ActionResult);
      await loadData();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Сервер игры недоступен.");
    } finally {
      setBusyItemId(null);
    }
  }

  return (
    <main>
      <section className="game-panel shop-panel">
        <Link className="back-link" href="/profile">← Профиль</Link>
        <p className="eyebrow game-eyebrow">Снаряжение</p>
        <h1 className="game-title">Инвентарь</h1>

        {profile && (
          <div className="inventory-resources">
            <span>Энергия <strong>{profile.energy.current}/{profile.energy.maximum}</strong></span>
            <span>Хладнокровие <strong>{profile.nerve.current}/{profile.nerve.maximum}</strong></span>
            <span>Здоровье <strong>{profile.health.current}/{profile.health.maximum}</strong></span>
          </div>
        )}

        {message && <p className={message.succeeded ? "action-result success" : "action-result failure"}>{message.message}</p>}
        {error && <p className="form-error">{error}</p>}

        <div className="item-grid">
          {items.length === 0 && <p className="field-hint">Инвентарь пуст. Предметы можно купить в магазине.</p>}
          {items.map((item) => (
            <article className="item-card" key={item.itemId}>
              <div>
                <h2>{item.name}</h2>
                <p>{item.description}</p>
              </div>
              <strong>В наличии: {item.quantity}</strong>
              <label>
                Использовать
                <input
                  type="number"
                  min="1"
                  max={Math.min(100, item.quantity)}
                  value={quantities[item.itemId] ?? 1}
                  onChange={(event) => setQuantities((current) => ({
                    ...current,
                    [item.itemId]: Number(event.target.value),
                  }))}
                />
              </label>
              <button className="primary-button" type="button" onClick={() => useItem(item)} disabled={busyItemId !== null}>
                {busyItemId === item.itemId ? "Использование…" : "Использовать"}
              </button>
            </article>
          ))}
        </div>

        <Link className="secondary-button inventory-link" href="/shop">В магазин</Link>
      </section>
    </main>
  );
}
