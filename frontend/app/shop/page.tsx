"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";

type ShopItem = {
  id: string;
  name: string;
  description: string;
  price: number;
  type: string;
  effectResource: string;
  effectAmount: number;
};

type Profile = { credits: number };
type ActionResult = { succeeded: boolean; message: string; credits: number };

export default function ShopPage() {
  const router = useRouter();
  const [items, setItems] = useState<ShopItem[]>([]);
  const [credits, setCredits] = useState<number | null>(null);
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [busyItemId, setBusyItemId] = useState<string | null>(null);
  const [message, setMessage] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");

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
      const response = await fetch(`${API_URL}/api/game/shop/buy`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ requestId: crypto.randomUUID(), itemId: item.id, quantity }),
      });
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
    } catch {
      setError("Сервер игры недоступен.");
    } finally {
      setBusyItemId(null);
    }
  }

  return (
    <main>
      <section className="game-panel shop-panel">
        <Link className="back-link" href="/profile">← Профиль</Link>
        <p className="eyebrow game-eyebrow">Магазин NPC</p>
        <h1 className="game-title">Технолавка</h1>
        <p className="lead compact-lead">Кредиты: <strong>{credits?.toLocaleString("ru-RU") ?? "…"}</strong></p>

        {message && <p className={message.succeeded ? "action-result success" : "action-result failure"}>{message.message}</p>}
        {error && <p className="form-error">{error}</p>}

        <div className="item-grid">
          {items.map((item) => (
            <article className="item-card" key={item.id}>
              <div>
                <h2>{item.name}</h2>
                <p>{item.description}</p>
              </div>
              <strong>{item.price} кредитов</strong>
              <label>
                Количество
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
              </button>
            </article>
          ))}
        </div>

        <Link className="secondary-button inventory-link" href="/inventory">Открыть инвентарь</Link>
      </section>
    </main>
  );
}
