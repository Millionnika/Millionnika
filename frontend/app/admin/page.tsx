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
type Contract = { id: string; name: string; description: string; costNerve: number; successChance: number; rewardCredits: number; rewardExperience: number; isActive: boolean; direction: "normal" | "hacking"; minimumLevel: number; minimumHackingLevel: number; rewardHackingExperience: number };
type Item = { id: string; name: string; description: string; price: number; type: string; effectResource: string; effectAmount: number; isActive: boolean; allowedSlots: string; minimumDamage: number; maximumDamage: number; minimumAccuracy: number; maximumAccuracy: number; minimumArmor: number; maximumArmor: number; armorCoverage: string };
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

  async function createContract(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(""); setMessage("");
    try {
      const response = await request("/api/admin/contracts", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(contractPayload(form)),
      });
      setMessage((await response.json() as { message: string }).message); await refresh();
    } catch (reason) { setError((reason as Error).message); }
  }

  async function createItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = new FormData(event.currentTarget);
    setError(""); setMessage("");
    try {
      const response = await request("/api/admin/items", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(itemCreatePayload(form)) });
      setMessage((await response.json() as { message: string }).message); await refresh();
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

      <AdminSection title="Контракты"><form className="admin-editor admin-form" onSubmit={createContract}><h3>Новый контракт</h3><label>Название<input name="name" maxLength={120} required /></label><label>Описание<textarea name="description" maxLength={500} required /></label><ContractProgressionFields /><NumberField name="costNerve" label="Стоимость" value={1} min={1} /><NumberField name="successChance" label="Базовый шанс (0–1)" value={1} min={0} max={1} step="0.01" /><NumberField name="rewardCredits" label="Награда" value={0} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardExperience" label="Общий опыт" value={0} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardHackingExperience" label="Опыт Взлома" value={0} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked /><ReasonField /><button className="primary-button" type="submit">Создать контракт</button></form>{contracts.map((contract) => <form className="admin-editor admin-form" key={contract.id} onSubmit={(event) => { event.preventDefault(); void mutate(`/api/admin/contracts/${contract.id}`, contractPayload(new FormData(event.currentTarget))); }}>
        <label>Название<input name="name" defaultValue={contract.name} maxLength={120} required /></label><label>Описание<textarea name="description" defaultValue={contract.description} maxLength={500} required /></label><ContractProgressionFields contract={contract} /><NumberField name="costNerve" label="Стоимость" value={contract.costNerve} min={1} /><NumberField name="successChance" label="Базовый шанс (0–1)" value={contract.successChance} min={0} max={1} step="0.01" /><NumberField name="rewardCredits" label="Награда" value={contract.rewardCredits} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardExperience" label="Общий опыт" value={contract.rewardExperience} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardHackingExperience" label="Опыт Взлома" value={contract.rewardHackingExperience} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked={contract.isActive} /><ReasonField /><button className="primary-button" type="submit">Сохранить контракт</button>
      </form>)}</AdminSection>

      <AdminSection title="Предметы">
        <form className="admin-editor admin-form" onSubmit={createItem}><h3>Новый предмет</h3><label>Название<input name="name" maxLength={120} required /></label><label>Описание<textarea name="description" maxLength={500} required /></label><label>Тип<select name="type" defaultValue="weapon"><option value="consumable">Расходник</option><option value="weapon">Оружие</option><option value="armor">Броня</option></select></label><label>Слот<select name="allowedSlots" defaultValue="primary"><option value="primary">Основное</option><option value="secondary">Вторичное</option><option value="melee">Ближний бой</option><option value="helmet">Голова</option><option value="body">Корпус</option><option value="pants">Ноги</option><option value="gloves">Перчатки</option><option value="boots">Обувь</option><option value="">Нет (расходник)</option></select></label><NumberField name="price" label="Цена" value={0} min={0} max={MAX_ECONOMY_VALUE} /><ItemDefinitionFields /><CheckField name="isActive" label="Активен" checked /><ReasonField /><button className="primary-button" type="submit">Создать предмет</button></form>
        {items.map((item) => <form className="admin-editor admin-form" key={item.id} onSubmit={(event) => { event.preventDefault(); void mutate(`/api/admin/items/${item.id}`, itemUpdatePayload(new FormData(event.currentTarget))); }}>
          <h3>{item.name}</h3><p>Тип: {item.type} · Слот: {item.allowedSlots || "нет"}. Тип и совместимость выданного предмета не меняются.</p><NumberField name="price" label="Цена" value={item.price} min={0} max={MAX_ECONOMY_VALUE} /><label>Описание<textarea name="description" defaultValue={item.description} maxLength={500} required /></label><ItemDefinitionFields item={item} /><CheckField name="isActive" label="Активен" checked={item.isActive} /><ReasonField /><button className="primary-button" type="submit">Сохранить предмет</button>
        </form>)}
      </AdminSection>

      <AdminSection title="Действия игроков"><LogTable rows={actions.map((log) => ({ id: log.id, date: log.createdAtUtc, title: `${log.username} · ${log.actionType}`, text: log.message }))} /></AdminSection>
      <AdminSection title="Изменения администратора"><LogTable rows={audits.map((log) => ({ id: log.id, date: log.createdAtUtc, title: `${log.adminUsername} · ${log.action}`, text: `${log.reason} | Было: ${log.oldValue} | Стало: ${log.newValue}` }))} /></AdminSection>
    </section></main>
  );
}

function AdminSection({ title, children }: Readonly<{ title: string; children: React.ReactNode }>) { return <section className="admin-section"><h2>{title}</h2>{children}</section>; }
function NumberField({ name, label, value, min, max, step }: Readonly<{ name: string; label: string; value: number; min?: number; max?: number; step?: string }>) { return <label>{label}<input name={name} type="number" defaultValue={value} min={min} max={max} step={step} required /></label>; }
function ReasonField() { return <label className="reason-field">Причина<input name="reason" minLength={3} maxLength={500} required /></label>; }
function CheckField({ name, label, checked }: Readonly<{ name: string; label: string; checked: boolean }>) { return <label className="check-field"><input name={name} type="checkbox" defaultChecked={checked} />{label}</label>; }
function ContractProgressionFields({ contract }: Readonly<{ contract?: Contract }>) { return <><label>Направление<select name="direction" defaultValue={contract?.direction ?? "normal"}><option value="normal">Обычный</option><option value="hacking">Взлом</option></select></label><NumberField name="minimumLevel" label="Минимальный общий уровень" value={contract?.minimumLevel ?? 1} min={1} max={20} /><NumberField name="minimumHackingLevel" label="Минимальный Взлом" value={contract?.minimumHackingLevel ?? 1} min={1} max={20} /></>; }
function contractPayload(form: FormData) { return { name: form.get("name"), description: form.get("description"), direction: form.get("direction"), minimumLevel: Number(form.get("minimumLevel")), minimumHackingLevel: Number(form.get("minimumHackingLevel")), costNerve: Number(form.get("costNerve")), successChance: Number(form.get("successChance")), rewardCredits: Number(form.get("rewardCredits")), rewardExperience: Number(form.get("rewardExperience")), rewardHackingExperience: Number(form.get("rewardHackingExperience")), isActive: form.get("isActive") === "on", reason: form.get("reason") }; }
function ItemDefinitionFields({ item }: Readonly<{ item?: Item }>) { return <><label>Ресурс расходника<select name="effectResource" defaultValue={item?.effectResource ?? "energy"}><option value="energy">Энергия</option><option value="nerve">Хладнокровие</option><option value="health">Здоровье</option><option value="">Нет (экипировка)</option></select></label><NumberField name="effectAmount" label="Сила расходника (0 для экипировки)" value={item?.effectAmount ?? 0} min={0} /><NumberField name="minimumDamage" label="Урон от" value={item?.minimumDamage ?? 0} min={0} max={1000} step="0.01" /><NumberField name="maximumDamage" label="Урон до" value={item?.maximumDamage ?? 0} min={0} max={1000} step="0.01" /><NumberField name="minimumAccuracy" label="Точность от" value={item?.minimumAccuracy ?? 0} min={0} max={100} step="0.01" /><NumberField name="maximumAccuracy" label="Точность до" value={item?.maximumAccuracy ?? 0} min={0} max={100} step="0.01" /><NumberField name="minimumArmor" label="Броня от" value={item?.minimumArmor ?? 0} min={0} max={100} step="0.01" /><NumberField name="maximumArmor" label="Броня до" value={item?.maximumArmor ?? 0} min={0} max={100} step="0.01" /><label>Покрытие брони (JSON)<textarea name="armorCoverage" defaultValue={item?.armorCoverage ?? "{}"} required /></label></>; }
function itemUpdatePayload(form: FormData) { return { price: Number(form.get("price")), description: form.get("description"), effectResource: form.get("effectResource"), effectAmount: Number(form.get("effectAmount")), minimumDamage: Number(form.get("minimumDamage")), maximumDamage: Number(form.get("maximumDamage")), minimumAccuracy: Number(form.get("minimumAccuracy")), maximumAccuracy: Number(form.get("maximumAccuracy")), minimumArmor: Number(form.get("minimumArmor")), maximumArmor: Number(form.get("maximumArmor")), armorCoverage: form.get("armorCoverage"), isActive: form.get("isActive") === "on", reason: form.get("reason") }; }
function itemCreatePayload(form: FormData) { return { name: form.get("name"), type: form.get("type"), allowedSlots: form.get("allowedSlots"), ...itemUpdatePayload(form) }; }
function LogTable({ rows }: Readonly<{ rows: { id: string; date: string; title: string; text: string }[] }>) { return <div className="action-log">{rows.map((row) => <article key={row.id}><span>{new Date(row.date).toLocaleString("ru-RU")} · {row.title}</span><p>{row.text}</p></article>)}</div>; }
