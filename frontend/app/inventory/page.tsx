"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";
import { GameNav } from "@/components/GameNav";

type InventoryItem = {
  itemId: string; instanceId: string | null; name: string; description: string; type: string;
  effectResource: string; effectAmount: number; quantity: number; damage: number; accuracy: number;
  armor: number; equippedSlot: string | null; allowedSlots: string; armorCoverage: string;
  caliber: string; clipSize: number; rateOfFireMin: number; rateOfFireMax: number;
  cooldownSeconds: number; cooldownGroup: string; cooldownAvailableAtUtc: string | null;
};
type ActionResult = { succeeded: boolean; message: string };
type InventoryFilter = "all" | "weapon" | "armor" | "ammunition" | "consumable" | "material";

const slotNames: Record<string, string> = {
  primary: "Основное оружие", secondary: "Вторичное оружие", melee: "Ближний бой", temporary: "Временное",
  helmet: "Голова", body: "Корпус", pants: "Ноги", gloves: "Перчатки", boots: "Обувь",
};
const weaponSlots = ["primary", "secondary", "melee", "temporary"] as const;
const armorSlots = ["helmet", "body", "pants", "gloves", "boots"] as const;
const filters: { value: InventoryFilter; label: string }[] = [
  { value: "all", label: "Все" }, { value: "weapon", label: "Оружие" }, { value: "armor", label: "Броня" },
  { value: "ammunition", label: "Боеприпасы" }, { value: "consumable", label: "Расходники" },
  { value: "material", label: "Материалы" },
];

export default function InventoryPage() {
  const router = useRouter();
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [message, setMessage] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState<InventoryFilter>("all");
  const [now, setNow] = useState(() => Date.now());

  const visibleItems = useMemo(() => items.filter((item) => filter === "all" || item.type === filter), [filter, items]);

  const loadData = useCallback(async () => {
    const inventoryResponse = await fetch(`${API_URL}/api/game/inventory`, { credentials: "include" });
    if (inventoryResponse.status === 401) { router.replace("/login"); return; }
    if (!inventoryResponse.ok) throw new Error("Inventory request failed");
    setItems((await inventoryResponse.json()) as InventoryItem[]);
  }, [router]);

  useEffect(() => { void loadData().catch(() => setError("Не удалось загрузить инвентарь.")); }, [loadData]);
  useEffect(() => { const timer = window.setInterval(() => setNow(Date.now()), 1000); return () => window.clearInterval(timer); }, []);

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
    const quantity = item.cooldownSeconds > 0 ? 1 : quantities[item.itemId] ?? 1;
    return perform("/api/game/inventory/use", "item_use", { itemId: item.itemId, quantity }, item.itemId);
  }

  return (
    <main><section className="game-panel shop-panel">
      <GameNav /><Link className="back-link" href="/profile">← Профиль</Link>
      <p className="eyebrow game-eyebrow">Снаряжение</p><h1 className="game-title">Инвентарь</h1>
      {message && <p className={message.succeeded ? "action-result success" : "action-result failure"}>{message.message}</p>}
      {error && <p className="form-error">{error}</p>}

      <EquipmentSection title="Одетое оружие" slots={weaponSlots} items={items} busy={busyKey !== null} onUnequip={(slot) => void perform("/api/game/inventory/unequip", "item_unequip", { slot }, slot)} />
      <EquipmentSection title="Одетая броня" slots={armorSlots} items={items} busy={busyKey !== null} onUnequip={(slot) => void perform("/api/game/inventory/unequip", "item_unequip", { slot }, slot)} />

      <h2 className="section-title inventory-section-title">Все вещи</h2>
      <div className="inventory-filters" role="group" aria-label="Фильтр инвентаря">
        {filters.map((entry) => <button className={filter === entry.value ? "active" : ""} type="button" key={entry.value} onClick={() => setFilter(entry.value)}>{entry.label}</button>)}
      </div>
      <div className="inventory-grid">
        {items.length === 0 && <p className="field-hint">Инвентарь пуст. Предметы можно купить в магазине.</p>}
        {items.length > 0 && visibleItems.length === 0 && <p className="field-hint">В этой категории вещей нет.</p>}
        {visibleItems.map((item) => {
          const key = item.instanceId ?? item.itemId;
          const slots = item.allowedSlots.split(",").filter(Boolean);
          const remaining = item.cooldownAvailableAtUtc ? Math.max(0, Math.ceil((Date.parse(item.cooldownAvailableAtUtc) - now) / 1000)) : 0;
          return <article className="inventory-card" key={key}>
            <div className={`inventory-item-icon ${item.type}`} aria-hidden="true">{typeIcon(item.type)}</div>
            <div className="inventory-card-body"><div className="inventory-card-heading"><h2>{item.name}</h2><strong>{item.type === "weapon" ? `Урон ${item.damage} · Точность ${item.accuracy}` : item.type === "armor" ? `Броня ${item.armor}` : `В наличии: ${item.quantity}`}</strong></div><p>{item.description}</p>
            {item.type === "ammunition" ? <p className="inventory-meta">Калибр: {item.caliber}</p> : item.type === "material" ? <p className="inventory-meta">Материал для будущих систем</p> : item.type === "consumable" ? <div className="inventory-consume">
              <label><span>Количество</span><input type="number" min="1" max={item.cooldownSeconds > 0 ? 1 : Math.min(100, item.quantity)} value={item.cooldownSeconds > 0 ? 1 : quantities[item.itemId] ?? 1} disabled={item.cooldownSeconds > 0} onChange={(event) => setQuantities((current) => ({ ...current, [item.itemId]: Number(event.target.value) }))} /></label>
              <button className="primary-button compact-action" type="button" onClick={() => void useItem(item)} disabled={busyKey !== null || remaining > 0}>{busyKey === item.itemId ? "Использование…" : "Использовать"}</button>
              {remaining > 0 && <span className="inventory-meta" role="status">Доступно через {Math.floor(remaining / 60)}:{String(remaining % 60).padStart(2, "0")}</span>}
            </div> : <>
              {item.type === "armor" && <p className="inventory-meta">Покрытие: {formatCoverage(item.armorCoverage)}</p>}
              <div className="inventory-card-actions">
              {item.equippedSlot && <span className="equipped-badge">Надето: {slotNames[item.equippedSlot]}</span>}
              {item.equippedSlot ?
                <button className="secondary-button compact-action" type="button" disabled={busyKey !== null} onClick={() => void perform("/api/game/inventory/unequip", "item_unequip", { slot: item.equippedSlot }, key)}>Снять</button> :
                slots.map((slot) => <button className="primary-button compact-action" type="button" key={slot} disabled={busyKey !== null} onClick={() => void perform("/api/game/inventory/equip", "item_equip", { instanceId: item.instanceId, slot }, key)}>Надеть: {slotNames[slot]}</button>)}
              </div>
            </>}
            </div>
          </article>;
        })}
      </div>
      <Link className="secondary-button inventory-link" href="/shop">В магазин</Link>
    </section></main>
  );
}

function EquipmentSection({ title, slots, items, busy, onUnequip }: Readonly<{ title: string; slots: readonly string[]; items: InventoryItem[]; busy: boolean; onUnequip: (slot: string) => void }>) {
  return <section className="equipment-section" aria-label={title}>
    <h2 className="section-title">{title}</h2>
    <div className={`equipped-grid ${slots.length === 5 ? "armour" : "weapons"}`}>
      {slots.map((slot) => {
        const item = items.find((entry) => entry.equippedSlot === slot);
        return <article className={`equipped-slot${item ? " filled" : ""}`} key={slot}>
          <span>{slotNames[slot]}</span>
          <div className={`equipment-silhouette ${item?.type ?? slot}`} aria-hidden="true">{item ? typeIcon(item.type) : "—"}</div>
          <strong>{item?.name ?? "Пусто"}</strong>
          {item && <small>{item.type === "weapon" ? `Урон ${item.damage} · Точность ${item.accuracy}` : `Броня ${item.armor}`}</small>}
          {item && <button className="secondary-button equipment-remove" type="button" disabled={busy} onClick={() => onUnequip(slot)}>Снять</button>}
        </article>;
      })}
    </div>
  </section>;
}

function formatCoverage(value: string) {
  const labels: Record<string, string> = { head: "голова", throat: "горло", heart: "сердце", chest: "грудь", stomach: "живот", arms: "руки", hands: "кисти", groin: "пах", legs: "ноги", feet: "ступни" };
  try { return Object.entries(JSON.parse(value) as Record<string, number>).map(([part, percent]) => `${labels[part] ?? part} ${percent}%`).join(", "); }
  catch { return "нет данных"; }
}

function typeIcon(type: string) { return ({ weapon: "W", armor: "A", ammunition: "•", consumable: "+", material: "◇" } as Record<string, string>)[type] ?? "?"; }
