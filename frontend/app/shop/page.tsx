"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";
import { GameNav } from "@/components/GameNav";

type ShopItem = {
  id: string;
  name: string;
  description: string;
  price: number;
  type: string;
  effectResource: string;
  effectAmount: number;
  allowedSlots: string;
  minimumDamage: number;
  maximumDamage: number;
  minimumAccuracy: number;
  maximumAccuracy: number;
  minimumArmor: number;
  maximumArmor: number;
  armorCoverage: string;
  caliber: string; clipSize: number; rateOfFireMin: number; rateOfFireMax: number;
};

type Profile = { credits: number };
type ActionResult = { succeeded: boolean; message: string; credits: number };
type Category = "all" | "weapon" | "armor" | "ammunition" | "consumable";

const categories: { value: Category; label: string }[] = [
  { value: "all", label: "Всё" }, { value: "weapon", label: "Оружие" }, { value: "armor", label: "Броня" },
  { value: "ammunition", label: "Боеприпасы" }, { value: "consumable", label: "Расходники" },
];

export default function ShopPage() {
  const router = useRouter();
  const [items, setItems] = useState<ShopItem[]>([]);
  const [credits, setCredits] = useState<number | null>(null);
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [busyItemId, setBusyItemId] = useState<string | null>(null);
  const [message, setMessage] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");
  const [category, setCategory] = useState<Category>("all");
  const [query, setQuery] = useState("");

  const visibleItems = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase("ru-RU");
    return items.filter((item) => (category === "all" || item.type === category) && (!normalized || item.name.toLocaleLowerCase("ru-RU").includes(normalized)));
  }, [category, items, query]);

  useEffect(() => {
    Promise.all([
      fetch(`${API_URL}/api/game/shop`, { credentials: "include" }),
      fetch(`${API_URL}/api/profile`, { credentials: "include" }),
    ]).then(async ([shopResponse, profileResponse]) => {
      if (shopResponse.status === 401 || profileResponse.status === 401) {
        router.replace("/login");
        return;
      }
      if (!shopResponse.ok || !profileResponse.ok) throw new Error("Shop request failed");
      setItems((await shopResponse.json()) as ShopItem[]);
      setCredits(((await profileResponse.json()) as Profile).credits);
    }).catch(() => setError("Не удалось загрузить магазин."));
  }, [router]);

  async function buy(item: ShopItem) {
    setError("");
    setMessage(null);
    setBusyItemId(item.id);
    const quantity = quantities[item.id] ?? 1;

    try {
      const pending = getPendingAction("shop_purchase", { itemId: item.id, quantity });
      const response = await fetch(`${API_URL}/api/game/shop/buy`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ requestId: pending.requestId, itemId: item.id, quantity }),
      });
      pending.receivedResponse();
      if (response.status === 401) {
        router.replace("/login");
        return;
      }
      if (response.status === 409) {
        const result = (await response.json()) as ActionResult;
        setMessage(result);
        setCredits(result.credits);
        return;
      }
      if (!response.ok) {
        setError(await getErrorMessage(response));
        return;
      }
      const result = (await response.json()) as ActionResult;
      setMessage(result);
      setCredits(result.credits);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Сервер игры недоступен.");
    } finally {
      setBusyItemId(null);
    }
  }

  return (
    <main>
      <section className="game-panel shop-panel">
        <GameNav />
        <Link className="back-link" href="/profile">← Профиль</Link>
        <p className="eyebrow game-eyebrow">Магазин NPC</p>
        <h1 className="game-title">Технолавка</h1>
        <p className="lead compact-lead">Доступно: <strong>{credits?.toLocaleString("ru-RU") ?? "…"} кредитов</strong></p>

        <div className="shop-tools">
          <div className="shop-categories" role="group" aria-label="Категория товаров">
            {categories.map((entry) => <button className={category === entry.value ? "active" : ""} type="button" key={entry.value} onClick={() => setCategory(entry.value)}>{entry.label}</button>)}
          </div>
          <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Поиск по названию" aria-label="Поиск товара" />
        </div>

        {message && <p className={message.succeeded ? "action-result success" : "action-result failure"}>{message.message}</p>}
        {error && <p className="form-error">{error}</p>}

        <div className="item-grid">
          {visibleItems.length === 0 && <p className="field-hint">По этому запросу товаров нет.</p>}
          {visibleItems.map((item) => (
            <article className="item-card" key={item.id}>
              <div className={`item-icon ${item.type}`} aria-hidden="true">{typeIcon(item.type)}</div>
              <div className="item-card-content">
                <div className="item-card-heading"><h2>{item.name}</h2><strong>{item.price.toLocaleString("ru-RU")} ₡</strong></div>
                {item.type === "weapon" && <p className="item-stats">Урон {formatRange(item.minimumDamage, item.maximumDamage)} · Точность {formatRange(item.minimumAccuracy, item.maximumAccuracy)}</p>}
                {item.type === "weapon" && item.clipSize > 0 && <p className="item-stats">Калибр {item.caliber} · Боеприпасы: {compatibleAmmo(items, item.caliber)}</p>}
                {item.type === "ammunition" && <p className="item-stats">Боеприпасы · калибр {item.caliber}</p>}
                {item.type === "armor" && <p className="item-stats">Броня {formatRange(item.minimumArmor, item.maximumArmor)} · {slotName(item.allowedSlots)}</p>}
                {item.type === "consumable" && <p className="item-stats">{resourceName(item.effectResource)} +{item.effectAmount}</p>}
                <details><summary>Описание и параметры</summary><p>{item.description}</p>{item.type === "weapon" && item.clipSize > 0 && <p>Магазин {item.clipSize} · выстрелов за ход {item.rateOfFireMin}–{item.rateOfFireMax}</p>}{item.type === "armor" && <p>Покрытие: {formatCoverage(item.armorCoverage)}</p>}</details>
              </div>
              <div className="item-purchase"><label><span>Количество</span>
                <input
                  type="number"
                  min="1"
                  max="100"
                  value={quantities[item.id] ?? 1}
                  onChange={(event) => setQuantities((current) => ({
                    ...current,
                    [item.id]: Number(event.target.value),
                  }))}
                />
              </label>
              <button className="primary-button" type="button" onClick={() => buy(item)} disabled={busyItemId !== null}>
                {busyItemId === item.id ? "Покупка…" : "Купить"}
              </button></div>
            </article>
          ))}
        </div>

        <Link className="secondary-button inventory-link" href="/inventory">Открыть инвентарь</Link>
      </section>
    </main>
  );
}

function formatRange(minimum: number, maximum: number) {
  return minimum === maximum ? minimum.toString() : `${minimum}–${maximum}`;
}

function slotName(slot: string) {
  return ({ helmet: "голова", body: "корпус", pants: "ноги", gloves: "перчатки", boots: "обувь", primary: "основное", secondary: "вторичное", melee: "ближний бой" } as Record<string, string>)[slot] ?? slot;
}

function formatCoverage(value: string) {
  const labels: Record<string, string> = { head: "голова", throat: "горло", heart: "сердце", chest: "грудь", stomach: "живот", arms: "руки", hands: "кисти", groin: "пах", legs: "ноги", feet: "ступни" };
  try { return Object.entries(JSON.parse(value) as Record<string, number>).map(([part, percent]) => `${labels[part] ?? part} ${percent}%`).join(", "); }
  catch { return "нет данных"; }
}

function compatibleAmmo(items: ShopItem[], caliber: string) {
  return items.filter((item) => item.type === "ammunition" && item.caliber === caliber).map((item) => item.name).join(", ") || "нет в продаже";
}

function typeIcon(type: string) { return ({ weapon: "W", armor: "A", ammunition: "•", consumable: "+" } as Record<string, string>)[type] ?? "?"; }
function resourceName(resource: string) { return ({ energy: "Энергия", nerve: "Хладнокровие", health: "Здоровье" } as Record<string, string>)[resource] ?? "Ресурс"; }
