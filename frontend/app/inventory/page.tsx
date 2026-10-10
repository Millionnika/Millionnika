"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";
import { GameNav } from "@/components/GameNav";

type InventoryItem = {
  itemId: string; instanceId: string | null; name: string; description: string; type: string;
  effectResource: string; effectAmount: number; quantity: number; damage: number; accuracy: number;
  armor: number; equippedSlot: string | null; allowedSlots: string; armorCoverage: string;
  caliber: string; clipSize: number; rateOfFireMin: number; rateOfFireMax: number;
};
type Resource = { current: number; maximum: number };
type Profile = { energy: Resource; nerve: Resource; health: Resource };
type ActionResult = { succeeded: boolean; message: string };

const slotNames: Record<string, string> = {
  primary: "Основное", secondary: "Вторичное", melee: "Ближний бой", temporary: "Временное",
  helmet: "Голова", body: "Корпус", pants: "Ноги", gloves: "Перчатки", boots: "Обувь",
};

export default function InventoryPage() {
  const router = useRouter();
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [message, setMessage] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");

  const loadData = useCallback(async () => {
    const [inventoryResponse, profileResponse] = await Promise.all([
      fetch(`${API_URL}/api/game/inventory`, { credentials: "include" }),
      fetch(`${API_URL}/api/profile`, { credentials: "include" }),
    ]);
    if (inventoryResponse.status === 401 || profileResponse.status === 401) { router.replace("/login"); return; }
    if (!inventoryResponse.ok || !profileResponse.ok) throw new Error("Inventory request failed");
    setItems((await inventoryResponse.json()) as InventoryItem[]);
    setProfile((await profileResponse.json()) as Profile);
  }, [router]);

  useEffect(() => { void loadData().catch(() => setError("Не удалось загрузить инвентарь.")); }, [loadData]);

  async function perform(path: string, actionType: string, parameters: object, busy: string) {
    setError(""); setMessage(null); setBusyKey(busy);
    try {
      const pending = getPendingAction(actionType, parameters);
      const response = await fetch(`${API_URL}${path}`, {
        method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include",
        body: JSON.stringify({ requestId: pending.requestId, ...parameters }),
      });
      pending.receivedResponse();
      if (response.status === 401) { router.replace("/login"); return; }
      if (response.status === 409) { setMessage((await response.json()) as ActionResult); await loadData(); return; }
      if (!response.ok) { setError(await getErrorMessage(response)); return; }
      setMessage((await response.json()) as ActionResult); await loadData();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Сервер игры недоступен.");
    } finally { setBusyKey(null); }
  }

  function useItem(item: InventoryItem) {
    const quantity = quantities[item.itemId] ?? 1;
    return perform("/api/game/inventory/use", "item_use", { itemId: item.itemId, quantity }, item.itemId);
  }

  return (
    <main><section className="game-panel shop-panel">
      <GameNav /><Link className="back-link" href="/profile">← Профиль</Link>
      <p className="eyebrow game-eyebrow">Снаряжение</p><h1 className="game-title">Инвентарь</h1>
      {profile && <div className="inventory-resources"><span>Энергия <strong>{profile.energy.current}/{profile.energy.maximum}</strong></span><span>Хладнокровие <strong>{profile.nerve.current}/{profile.nerve.maximum}</strong></span><span>Здоровье <strong>{profile.health.current}/{profile.health.maximum}</strong></span></div>}
      {message && <p className={message.succeeded ? "action-result success" : "action-result failure"}>{message.message}</p>}
      {error && <p className="form-error">{error}</p>}
      <div className="item-grid">
        {items.length === 0 && <p className="field-hint">Инвентарь пуст. Предметы можно купить в магазине.</p>}
        {items.map((item) => {
          const key = item.instanceId ?? item.itemId;
          const slots = item.allowedSlots.split(",").filter(Boolean);
          return <article className="item-card" key={key}>
            <div><h2>{item.name}</h2><p>{item.description}</p></div>
            {item.type === "ammunition" ? <><strong>В наличии: {item.quantity}</strong><p className="field-hint">Калибр: {item.caliber}</p></> : item.type === "consumable" ? <>
              <strong>В наличии: {item.quantity}</strong>
              <label>Использовать<input type="number" min="1" max={Math.min(100, item.quantity)} value={quantities[item.itemId] ?? 1} onChange={(event) => setQuantities((current) => ({ ...current, [item.itemId]: Number(event.target.value) }))} /></label>
              <button className="primary-button" type="button" onClick={() => void useItem(item)} disabled={busyKey !== null}>{busyKey === item.itemId ? "Использование…" : "Использовать"}</button>
            </> : <>
              <strong>{item.type === "weapon" ? `Урон ${item.damage} · Точность ${item.accuracy}` : `Броня ${item.armor}`}</strong>
              {item.type === "armor" && <p className="field-hint">Покрытие: {formatCoverage(item.armorCoverage)}</p>}
              {item.equippedSlot && <p className="action-result success">Надето: {slotNames[item.equippedSlot]}</p>}
              {item.equippedSlot ?
                <button className="secondary-button" type="button" disabled={busyKey !== null} onClick={() => void perform("/api/game/inventory/unequip", "item_unequip", { slot: item.equippedSlot }, key)}>Снять</button> :
                slots.map((slot) => <button className="primary-button" type="button" key={slot} disabled={busyKey !== null} onClick={() => void perform("/api/game/inventory/equip", "item_equip", { instanceId: item.instanceId, slot }, key)}>Экипировать: {slotNames[slot]}</button>)}
            </>}
          </article>;
        })}
      </div>
      <Link className="secondary-button inventory-link" href="/shop">В магазин</Link>
    </section></main>
  );
}

function formatCoverage(value: string) {
  const labels: Record<string, string> = { head: "голова", throat: "горло", heart: "сердце", chest: "грудь", stomach: "живот", arms: "руки", hands: "кисти", groin: "пах", legs: "ноги", feet: "ступни" };
  try { return Object.entries(JSON.parse(value) as Record<string, number>).map(([part, percent]) => `${labels[part] ?? part} ${percent}%`).join(", "); }
  catch { return "нет данных"; }
}
