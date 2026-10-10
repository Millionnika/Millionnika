"use client";

import Link from "next/link";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";

type Player = { id: string; username: string; email: string; role: string; isBlocked: boolean };
type PlayerDetails = Player & {
  level: number; credits: number; energy: number; maxEnergy: number; nerve: number; maxNerve: number;
  health: number; maxHealth: number; strength: number; speed: number; defense: number; dexterity: number; experience: number;
};
type Contract = { id: string; name: string; costNerve: number; successChance: number; rewardCredits: number; rewardExperience: number; isActive: boolean };
type Item = { id: string; name: string; description: string; price: number; type: string; effectResource: string; effectAmount: number; isActive: boolean };
type ActionLog = { id: string; username: string; actionType: string; succeeded: boolean; message: string; createdAtUtc: string };
type AuditLog = { id: string; adminUsername: string; action: string; targetType: string; targetId: string; oldValue: string; newValue: string; reason: string; createdAtUtc: string };
const MAX_ECONOMY_VALUE = 1_000_000_000_000;

export default function AdminPage() {
  const router = useRouter();
  const [players, setPlayers] = useState<Player[]>([]);
  const [selected, setSelected] = useState<PlayerDetails | null>(null);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [actions, setActions] = useState<ActionLog[]>([]);
  const [audits, setAudits] = useState<AuditLog[]>([]);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const request = useCallback(async (path: string, init?: RequestInit) => {
    const response = await fetch(`${API_URL}${path}`, { ...init, credentials: "include" });
    if (response.status === 401) { router.replace("/login"); throw new Error("unauthorized"); }
    if (response.status === 403) { router.replace("/profile"); throw new Error("forbidden"); }
    if (!response.ok) throw new Error(await getErrorMessage(response));
    return response;
  }, [router]);

  const refresh = useCallback(async () => {
    const [p, c, i, a, audit] = await Promise.all([
      request("/api/admin/players"), request("/api/admin/contracts"), request("/api/admin/items"),
      request("/api/admin/action-logs"), request("/api/admin/audit-logs"),
    ]);
    setPlayers(await p.json()); setContracts(await c.json()); setItems(await i.json());
    setActions(await a.json()); setAudits(await audit.json());
  }, [request]);

  useEffect(() => { void refresh().catch((reason) => setError(reason.message)); }, [refresh]);

  async function openPlayer(id: string) {
    try { setSelected(await (await request(`/api/admin/players/${id}`)).json()); }
    catch (reason) { setError((reason as Error).message); }
  }

  async function mutate(path: string, body: object) {
    setError(""); setMessage("");
    try {
      const response = await request(path, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      const result = await response.json() as { message: string };
      setMessage(result.message);
      await refresh();
      if (selected) await openPlayer(selected.id);
    } catch (reason) { setError((reason as Error).message); }
  }

  function updateResources(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    void mutate(`/api/admin/players/${selected!.id}/resources`, {
      credits: Number(form.get("credits")), energy: Number(form.get("energy")),
      nerve: Number(form.get("nerve")), health: Number(form.get("health")), reason: form.get("reason"),
    });
  }

  return (
    <main><section className="admin-panel">
      <header className="profile-header"><div><p className="eyebrow">Закрытый раздел</p><h1 className="admin-title">Админка</h1></div><Link className="secondary-button" href="/profile">Профиль</Link></header>
      {message && <p className="action-result success">{message}</p>}
      {error && <p className="form-error">{error}</p>}

      <AdminSection title="Игроки">
        <div className="admin-list">{players.map((player) => <button type="button" key={player.id} onClick={() => openPlayer(player.id)} className="admin-row"><span>{player.username} · {player.email}</span><strong>{player.role}{player.isBlocked ? " · заблокирован" : ""}</strong></button>)}</div>
        {selected && <div className="admin-editor">
          <h3>{selected.username}</h3>
          <p>Уровень {selected.level} · Опыт {selected.experience} · Сила {selected.strength} · Скорость {selected.speed} · Защита {selected.defense} · Ловкость {selected.dexterity}</p>
          <form className="admin-form" onSubmit={updateResources}>
            <NumberField name="credits" label="Кредиты" value={selected.credits} min={0} max={MAX_ECONOMY_VALUE} />
            <NumberField name="energy" label={`Энергия (до ${selected.maxEnergy})`} value={selected.energy} min={0} max={selected.maxEnergy} />
            <NumberField name="nerve" label={`Хладнокровие (до ${selected.maxNerve})`} value={selected.nerve} min={0} max={selected.maxNerve} />
            <NumberField name="health" label={`Здоровье (до ${selected.maxHealth})`} value={selected.health} min={0} max={selected.maxHealth} />
            <ReasonField /><button className="primary-button" type="submit">Сохранить ресурсы</button>
          </form>
          <form className="admin-inline-form" onSubmit={(event) => { event.preventDefault(); const form = new FormData(event.currentTarget); void mutate(`/api/admin/players/${selected.id}/block`, { isBlocked: !selected.isBlocked, reason: form.get("reason") }); }}>
            <ReasonField /><button className="secondary-button" type="submit">{selected.isBlocked ? "Разблокировать" : "Заблокировать"}</button>
          </form>
        </div>}
      </AdminSection>

      <AdminSection title="Контракты">{contracts.map((contract) => <form className="admin-editor admin-form" key={contract.id} onSubmit={(event) => { event.preventDefault(); const f = new FormData(event.currentTarget); void mutate(`/api/admin/contracts/${contract.id}`, { costNerve: Number(f.get("costNerve")), successChance: Number(f.get("successChance")), rewardCredits: Number(f.get("rewardCredits")), rewardExperience: Number(f.get("rewardExperience")), isActive: f.get("isActive") === "on", reason: f.get("reason") }); }}>
        <h3>{contract.name}</h3><NumberField name="costNerve" label="Стоимость" value={contract.costNerve} min={1} /><NumberField name="successChance" label="Шанс (0–1)" value={contract.successChance} min={0} max={1} step="0.01" /><NumberField name="rewardCredits" label="Награда" value={contract.rewardCredits} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardExperience" label="Опыт" value={contract.rewardExperience} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked={contract.isActive} /><ReasonField /><button className="primary-button" type="submit">Сохранить контракт</button>
      </form>)}</AdminSection>

      <AdminSection title="Предметы">{items.map((item) => <form className="admin-editor admin-form" key={item.id} onSubmit={(event) => { event.preventDefault(); const f = new FormData(event.currentTarget); void mutate(`/api/admin/items/${item.id}`, { price: Number(f.get("price")), description: f.get("description"), effectResource: f.get("effectResource"), effectAmount: Number(f.get("effectAmount")), isActive: f.get("isActive") === "on", reason: f.get("reason") }); }}>
        <h3>{item.name}</h3><NumberField name="price" label="Цена" value={item.price} min={0} max={MAX_ECONOMY_VALUE} /><label>Описание<textarea name="description" defaultValue={item.description} maxLength={500} required /></label><label>Ресурс<select name="effectResource" defaultValue={item.effectResource}><option value="energy">Энергия</option><option value="nerve">Хладнокровие</option><option value="health">Здоровье</option></select></label><NumberField name="effectAmount" label="Сила эффекта" value={item.effectAmount} min={1} /><CheckField name="isActive" label="Активен" checked={item.isActive} /><ReasonField /><button className="primary-button" type="submit">Сохранить предмет</button>
      </form>)}</AdminSection>

      <AdminSection title="Действия игроков"><LogTable rows={actions.map((log) => ({ id: log.id, date: log.createdAtUtc, title: `${log.username} · ${log.actionType}`, text: log.message }))} /></AdminSection>
      <AdminSection title="Изменения администратора"><LogTable rows={audits.map((log) => ({ id: log.id, date: log.createdAtUtc, title: `${log.adminUsername} · ${log.action}`, text: `${log.reason} | Было: ${log.oldValue} | Стало: ${log.newValue}` }))} /></AdminSection>
    </section></main>
  );
}

function AdminSection({ title, children }: Readonly<{ title: string; children: React.ReactNode }>) { return <section className="admin-section"><h2>{title}</h2>{children}</section>; }
function NumberField({ name, label, value, min, max, step }: Readonly<{ name: string; label: string; value: number; min?: number; max?: number; step?: string }>) { return <label>{label}<input name={name} type="number" defaultValue={value} min={min} max={max} step={step} required /></label>; }
function ReasonField() { return <label className="reason-field">Причина<input name="reason" minLength={3} maxLength={500} required /></label>; }
function CheckField({ name, label, checked }: Readonly<{ name: string; label: string; checked: boolean }>) { return <label className="check-field"><input name={name} type="checkbox" defaultChecked={checked} />{label}</label>; }
function LogTable({ rows }: Readonly<{ rows: { id: string; date: string; title: string; text: string }[] }>) { return <div className="action-log">{rows.map((row) => <article key={row.id}><span>{new Date(row.date).toLocaleString("ru-RU")} · {row.title}</span><p>{row.text}</p></article>)}</div>; }
