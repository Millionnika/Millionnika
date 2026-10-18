"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { GameNav } from "@/components/GameNav";

type Player = { id: string; username: string; email: string; role: string; isBlocked: boolean };
type PlayerDetails = Player & {
  level: number; credits: number; energy: number; maxEnergy: number; nerve: number; maxNerve: number;
  health: number; maxHealth: number; strength: number; reflexes: number; toughness: number; agility: number; experience: number;
};
type Contract = { id: string; name: string; description: string; costNerve: number; successChance: number; rewardCredits: number; rewardExperience: number; isActive: boolean; direction: "normal" | "hacking"; minimumLevel: number; minimumHackingLevel: number; rewardHackingExperience: number };
type Item = { id: string; name: string; description: string; price: number; type: string; effectResource: string; effectAmount: number; cooldownSeconds: number; cooldownGroup: string; isActive: boolean; allowedSlots: string; minimumDamage: number; maximumDamage: number; minimumAccuracy: number; maximumAccuracy: number; minimumArmor: number; maximumArmor: number; armorCoverage: string; caliber: string; clipSize: number; rateOfFireMin: number; rateOfFireMax: number };
const ITEM_CATEGORIES = [["all", "Все"], ["weapon", "Оружие"], ["armor", "Броня"], ["consumable", "Расходники"], ["ammunition", "Боеприпасы"], ["material", "Материалы"]] as const;
type ItemCategory = (typeof ITEM_CATEGORIES)[number][0];
const ITEM_TYPE_LABELS: Record<string, string> = { weapon: "Оружие", armor: "Броня", consumable: "Расходник", ammunition: "Боеприпасы", material: "Материал" };
type ActionLog = { id: string; username: string; actionType: string; succeeded: boolean; message: string; createdAtUtc: string };
type AuditLog = { id: string; adminUsername: string; action: string; targetType: string; targetId: string; oldValue: string; newValue: string; reason: string; createdAtUtc: string };
type DisplayLog = { id: string; date: string; title: string; comment: string | null; technical?: AuditLog };
type HuntingLocation = { id: string; name: string; description: string; minimumHuntingLevel: number; isActive: boolean };
type HuntingTarget = { id: string; name: string; description: string; minimumHuntingLevel: number; energyCost: number; health: number; strength: number; reflexes: number; toughness: number; agility: number; weaponName: string; weaponDamage: number; weaponAccuracy: number; clipSize: number; rateOfFireMin: number; rateOfFireMax: number; spareClips: number; armor: number; armorCoverage: string; rewardCredits: number; minimumCredits: number; maximumCredits: number; lootRows: { itemId: string; itemName: string; chancePercent: number; minimumQuantity: number; maximumQuantity: number }[]; rewardExperience: number; rewardHuntingExperience: number; isActive: boolean };
type HuntingAssignment = { locationId: string; npcId: string; name: string; category: string; isActive: boolean };
type NpcCategory = "peaceful" | "robot" | "mutant" | "animal";
type NpcZone = { coverage: number; protection: number };
type NpcLootRow = { itemId: string; itemName: string; chancePercent: number; minimumQuantity: number; maximumQuantity: number };
type Npc = { id: string; source: "template" | "hunting"; category: NpcCategory; name: string; description: string; isActive: boolean; portraitPath: string | null; figurePath: string | null; health: number | null; strength: number | null; toughness: number | null; reflexes: number | null; agility: number | null; weaponName: string | null; weaponDamage: number | null; weaponAccuracy: number | null; armor: number | null; armorCoverage: string | null; armorZones: Record<string, NpcZone>; lootRows: NpcLootRow[] };
const BODY_ZONES = [["head", "Голова"], ["throat", "Горло"], ["heart", "Сердце"], ["chest", "Грудь"], ["stomach", "Живот"], ["arms", "Руки"], ["hands", "Кисти"], ["groin", "Пах"], ["legs", "Ноги"], ["feet", "Ступни"]] as const;
const MAX_ECONOMY_VALUE = 1_000_000_000_000;
type AdminTab = "players" | "items" | "activities" | "npcs" | "logs";
type ActivityTab = "contracts" | "hacking" | "hunting";
type HuntingTab = "locations" | "targets";

export default function AdminPage() {
  const router = useRouter();
  const [players, setPlayers] = useState<Player[]>([]);
  const [selected, setSelected] = useState<PlayerDetails | null>(null);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [actions, setActions] = useState<ActionLog[]>([]);
  const [audits, setAudits] = useState<AuditLog[]>([]);
  const [huntingLocations, setHuntingLocations] = useState<HuntingLocation[]>([]);
  const [huntingTargets, setHuntingTargets] = useState<HuntingTarget[]>([]);
  const [huntingAssignments, setHuntingAssignments] = useState<HuntingAssignment[]>([]);
  const [npcs, setNpcs] = useState<Npc[]>([]);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [tab, setTab] = useState<AdminTab>("players");
  const [activityTab, setActivityTab] = useState<ActivityTab>("contracts");
  const [huntingTab, setHuntingTab] = useState<HuntingTab>("locations");
  const [npcCategory, setNpcCategory] = useState<NpcCategory>("peaceful");
  const [selectedNpcId, setSelectedNpcId] = useState<string | null>(null);
  const [selectedLocationId, setSelectedLocationId] = useState<string | null>(null);
  const [assignmentNpcId, setAssignmentNpcId] = useState("");
  const [selectedTargetId, setSelectedTargetId] = useState<string | null>(null);
  const [selectedContractId, setSelectedContractId] = useState<string | null>(null);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [itemCategory, setItemCategory] = useState<ItemCategory>("all");
  const [itemSearch, setItemSearch] = useState("");
  const [selectedLogId, setSelectedLogId] = useState<string | null>(null);
  const [logTab, setLogTab] = useState<"actions" | "audits">("actions");

  const request = useCallback(async (path: string, init?: RequestInit) => {
    const response = await fetch(`${API_URL}${path}`, { ...init, credentials: "include" });
    if (response.status === 401) { router.replace("/login"); throw new Error("unauthorized"); }
    if (response.status === 403) { router.replace("/profile"); throw new Error("forbidden"); }
    if (!response.ok) throw new Error(await getErrorMessage(response));
    return response;
  }, [router]);

  const refresh = useCallback(async () => {
    const [p, c, i, a, audit, locations, targets, npcResponse, assignments] = await Promise.all([
      request("/api/admin/players"), request("/api/admin/contracts"), request("/api/admin/items"),
      request("/api/admin/action-logs"), request("/api/admin/audit-logs"),
      request("/api/admin/hunting/locations"), request("/api/admin/hunting/targets"), request("/api/admin/npcs"),
      request("/api/admin/hunting/assignments"),
    ]);
    setPlayers(await p.json()); setContracts(await c.json()); setItems(await i.json());
    setActions(await a.json()); setAudits(await audit.json());
    setHuntingLocations(await locations.json()); setHuntingTargets(await targets.json());
    setNpcs(await npcResponse.json());
    setHuntingAssignments(await assignments.json());
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

  async function saveNpc(event: FormEvent<HTMLFormElement>, id?: string) {
    event.preventDefault(); setError(""); setMessage("");
    try {
      const body = npcPayload(new FormData(event.currentTarget), npcCategory);
      const response = await request(id ? `/api/admin/npcs/${id}` : "/api/admin/npcs", {
        method: id ? "PUT" : "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body),
      });
      setMessage((await response.json() as { message: string }).message);
      await refresh();
      if (!id) setSelectedNpcId(null);
    } catch (reason) { setError((reason as Error).message); }
  }

  async function changeAssignment(path: string, method: "PUT" | "DELETE", body: object) {
    setError(""); setMessage("");
    try {
      const response = await request(path, { method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      setMessage((await response.json() as { message: string }).message);
      setAssignmentNpcId("");
      await refresh();
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

  const visibleContracts = contracts.filter((contract) => contract.direction === (activityTab === "hacking" ? "hacking" : "normal"));
  const selectedLocation = huntingLocations.find((location) => location.id === selectedLocationId);
  const assignedHere = huntingAssignments.filter((assignment) => assignment.locationId === selectedLocationId);
  const availableNpcs = npcs.filter((npc) => npc.category !== "peaceful" && npc.health !== null &&
    !assignedHere.some((assignment) => assignment.npcId === npc.id));
  const assignmentNpc = availableNpcs.find((npc) => npc.id === assignmentNpcId);
  const selectedTarget = huntingTargets.find((target) => target.id === selectedTargetId);
  const selectedContract = visibleContracts.find((contract) => contract.id === selectedContractId);
  const visibleItems = items.filter((item) => (itemCategory === "all" || item.type === itemCategory) &&
    item.name.toLocaleLowerCase("ru-RU").includes(itemSearch.trim().toLocaleLowerCase("ru-RU")));
  const selectedItem = visibleItems.find((item) => item.id === selectedItemId);
  const selectedNpc = npcs.find((npc) => npc.id === selectedNpcId && npc.category === npcCategory);
  const visibleLogs: DisplayLog[] = logTab === "actions"
    ? actions.map((log) => ({ id: log.id, date: log.createdAtUtc, title: `${log.username} · ${log.actionType}`, comment: log.message }))
    : audits.map((log) => formatAdminAudit(log, huntingLocations, npcs));
  const selectedLog = visibleLogs.find((log) => log.id === selectedLogId);

  return (
    <main><section className="admin-panel">
      <GameNav />
      <header className="profile-header"><div><p className="eyebrow">Закрытый раздел</p><h1 className="admin-title">Админка</h1></div></header>
      {message && <p className="action-result success">{message}</p>}
      {error && <p className="form-error">{error}</p>}
      <nav className="admin-tabs" aria-label="Разделы админки">
        {([ ["players", "Игроки"], ["items", "Предметы"], ["activities", "Активности"], ["npcs", "НПЦ"], ["logs", "Журналы"] ] as const).map(([value, label]) => <button key={value} type="button" className={tab === value ? "selected" : ""} aria-current={tab === value ? "page" : undefined} onClick={() => { setTab(value); setMessage(""); setError(""); }}>{label}</button>)}
      </nav>
      {tab === "activities" && <nav className="admin-subtabs" aria-label="Активности">
        {([ ["contracts", "Контракты"], ["hacking", "Взлом"], ["hunting", "Охота"] ] as const).map(([value, label]) => <button key={value} type="button" className={activityTab === value ? "selected" : ""} aria-current={activityTab === value ? "page" : undefined} onClick={() => { setActivityTab(value); setSelectedContractId(null); }}>{label}</button>)}
      </nav>}
      {tab === "activities" && activityTab === "hunting" && <nav className="admin-subtabs admin-subtabs-nested" aria-label="Охота">
        {([ ["locations", "Локации"], ["targets", "Роботы"] ] as const).map(([value, label]) => <button key={value} type="button" className={huntingTab === value ? "selected" : ""} aria-current={huntingTab === value ? "page" : undefined} onClick={() => setHuntingTab(value)}>{label}</button>)}
      </nav>}

      {tab === "players" && <AdminSection title="Игроки">
        <div className="admin-list">{players.map((player) => <button type="button" key={player.id} onClick={() => openPlayer(player.id)} className={`admin-row${selected?.id === player.id ? " selected" : ""}`}><span>{player.username} · {player.email}</span><strong>{player.role}{player.isBlocked ? " · заблокирован" : ""}</strong></button>)}</div>
        {selected && <div className="admin-editor">
          <h3>{selected.username}</h3>
          <p>Уровень {selected.level} · Опыт {selected.experience} · Сила (STR) {selected.strength} · Стойкость (TGH) {selected.toughness} · Реакция (REF) {selected.reflexes} · Ловкость (AGI) {selected.agility}</p>
          <form className="admin-form" onSubmit={updateResources}>
            <NumberField name="credits" label="Кредиты" value={selected.credits} min={0} max={MAX_ECONOMY_VALUE} />
            <NumberField name="energy" label={`Энергия (до ${selected.maxEnergy})`} value={selected.energy} min={0} max={selected.maxEnergy} />
            <NumberField name="nerve" label={`Хладнокровие (до ${selected.maxNerve})`} value={selected.nerve} min={0} max={selected.maxNerve} />
            <NumberField name="health" label={`Здоровье (до ${selected.maxHealth})`} value={selected.health} min={0} max={selected.maxHealth} />
            <AdminActions><ReasonField /><button className="primary-button" type="submit">Сохранить ресурсы</button></AdminActions>
          </form>
          <form className="admin-inline-form" onSubmit={(event) => { event.preventDefault(); const form = new FormData(event.currentTarget); void mutate(`/api/admin/players/${selected.id}/block`, { isBlocked: !selected.isBlocked, reason: form.get("reason") }); }}>
            <ReasonField /><button className="secondary-button" type="submit">{selected.isBlocked ? "Разблокировать" : "Заблокировать"}</button>
          </form>
        </div>}
      </AdminSection>}

      {tab === "activities" && activityTab === "hunting" && huntingTab === "locations" && <AdminSection title="Локации">
        <AdminRecordList rows={huntingLocations.map((location) => ({ id: location.id, title: location.name, detail: `Уровень охоты ${location.minimumHuntingLevel}` }))} selectedId={selectedLocationId} onSelect={setSelectedLocationId} />
        {selectedLocation && <div className="admin-location-content" key={selectedLocation.id}>
        <form className="admin-editor admin-form" onSubmit={(event) => { event.preventDefault(); const form = new FormData(event.currentTarget); void mutate(`/api/admin/hunting/locations/${selectedLocation.id}`, { name: form.get("name"), description: form.get("description"), minimumHuntingLevel: Number(form.get("minimumHuntingLevel")), isActive: form.get("isActive") === "on", reason: form.get("reason") }); }}>
          <h3>{selectedLocation.name}</h3><AdminGroup title="Основное" wide><label>Название<input name="name" defaultValue={selectedLocation.name} maxLength={120} required /></label><label>Описание<textarea name="description" defaultValue={selectedLocation.description} maxLength={500} required /></label></AdminGroup><AdminGroup title="Доступность"><NumberField name="minimumHuntingLevel" label="Уровень охоты" value={selectedLocation.minimumHuntingLevel} min={1} max={20} /><CheckField name="isActive" label="Активна" checked={selectedLocation.isActive} /></AdminGroup><AdminActions><ReasonField /><button className="primary-button" type="submit">Сохранить локацию</button></AdminActions>
        </form>
        <div className="admin-editor">
          <h3>НПЦ в локации «{selectedLocation.name}»</h3>
          {assignedHere.length === 0 && <p>Пока никто не назначен.</p>}
          <div className="admin-assignment-list">{assignedHere.map((assignment) =>
            <form className="admin-inline-form" key={assignment.npcId} onSubmit={(event) => {
              event.preventDefault(); const form = new FormData(event.currentTarget);
              void changeAssignment(`/api/admin/hunting/locations/${selectedLocation.id}/npcs/${assignment.npcId}`,
                "DELETE", { comment: form.get("comment") });
            }}><span>{assignment.name} · {npcCategoryLabel(assignment.category)}{assignment.isActive ? "" : " · отключён"}</span>
              <CommentField /><button className="secondary-button" type="submit">Убрать из локации</button>
            </form>)}</div>
          {availableNpcs.length === 0 && <p>Все доступные боевые НПЦ уже добавлены</p>}
          {availableNpcs.length > 0 && <form className="admin-form" onSubmit={(event) => {
            event.preventDefault(); const form = new FormData(event.currentTarget);
            const npc = availableNpcs.find((value) => value.id === String(form.get("npcId")));
            if (!npc) return;
            const setup = npc.source === "template" ? huntingSetupPayload(form) : null;
            void changeAssignment(`/api/admin/hunting/locations/${selectedLocation.id}/npcs`, "PUT",
              { npcId: npc.id, setup, comment: form.get("comment") });
          }}>
            <label>Боевой НПЦ<select name="npcId" value={assignmentNpcId} onChange={(event) => setAssignmentNpcId(event.target.value)} required>
              <option value="">Выберите НПЦ</option>
              {availableNpcs.map((npc) => <option key={npc.id} value={npc.id}>{npc.name} · {npcCategoryLabel(npc.category)}{npc.source === "template" ? " · без настроек охоты" : ""}</option>)}
            </select></label>
            {assignmentNpc?.source === "template" && <HuntingSetupFields />}
            {assignmentNpc && <AdminActions><CommentField /><button className="primary-button" type="submit">Добавить</button></AdminActions>}
          </form>}
        </div></div>}
      </AdminSection>}

      {tab === "activities" && activityTab === "hunting" && huntingTab === "targets" && <AdminSection title="Роботы">
        <AdminRecordList rows={huntingTargets.map((target) => ({ id: target.id, title: target.name, detail: `Уровень охоты ${target.minimumHuntingLevel}` }))} selectedId={selectedTargetId} onSelect={setSelectedTargetId} />
        {selectedTarget && [selectedTarget].map((target) => <form className="admin-editor admin-form" key={target.id} onSubmit={(event) => { event.preventDefault(); void mutate(`/api/admin/hunting/targets/${target.id}`, huntingTargetPayload(new FormData(event.currentTarget), target)); }}>
          <h3>{target.name}</h3><AdminGroup title="Основное" wide><label>Название<input name="name" defaultValue={target.name} maxLength={120} required /></label><label>Описание<textarea name="description" defaultValue={target.description} maxLength={500} required /></label></AdminGroup>
          <AdminGroup title="Доступ и здоровье"><NumberField name="minimumHuntingLevel" label="Уровень охоты" value={target.minimumHuntingLevel} min={1} max={20} /><NumberField name="energyCost" label="Энергия" value={target.energyCost} min={1} /><NumberField name="health" label="Здоровье" value={target.health} min={1} /></AdminGroup>
          <AdminGroup title="Характеристики">
          {(["strength","toughness","reflexes","agility"] as const).map((name) => <NumberField key={name} name={name} label={{ strength: "Сила (STR)", toughness: "Стойкость (TGH)", reflexes: "Реакция (REF)", agility: "Ловкость (AGI)" }[name]} value={target[name]} min={1} />)}
          </AdminGroup><AdminGroup title="Оружие и защита">
          <label>Оружие<input name="weaponName" defaultValue={target.weaponName} maxLength={120} required /></label><NumberField name="weaponDamage" label="Damage" value={target.weaponDamage} min={1} max={1000} step="0.01" /><NumberField name="weaponAccuracy" label="Accuracy" value={target.weaponAccuracy} min={0} max={100} step="0.01" />
          <NumberField name="clipSize" label="Магазин" value={target.clipSize} min={0} max={1000} /><NumberField name="rateOfFireMin" label="RoF от" value={target.rateOfFireMin} min={0} max={1000} /><NumberField name="rateOfFireMax" label="RoF до" value={target.rateOfFireMax} min={0} max={1000} /><NumberField name="spareClips" label="Запасных магазинов" value={target.spareClips} min={0} max={3} />
          <p>Защита по зонам настраивается в <button type="button" className="text-button" onClick={() => { const npc = npcs.find((value) => value.id === target.id); if (npc) { setNpcCategory(npc.category); setSelectedNpcId(npc.id); setTab("npcs"); } }}>карточке НПЦ</button>.</p></AdminGroup>
          <AdminGroup title="Награды и добыча"><NumberField name="rewardCredits" label="Базовые кредиты" value={target.rewardCredits} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="minimumCredits" label="Дополнительные кредиты от" value={target.minimumCredits} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="maximumCredits" label="Дополнительные кредиты до" value={target.maximumCredits} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardExperience" label="Общий опыт" value={target.rewardExperience} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardHuntingExperience" label="Опыт охоты" value={target.rewardHuntingExperience} min={0} max={MAX_ECONOMY_VALUE} /><p>Добыча настраивается в <button type="button" className="text-button" onClick={() => { const npc = npcs.find((value) => value.id === target.id); if (npc) { setNpcCategory(npc.category); setSelectedNpcId(npc.id); setTab("npcs"); } }}>карточке НПЦ «{target.name}»</button>.</p><CheckField name="isActive" label="Активен" checked={target.isActive} /></AdminGroup><AdminActions><ReasonField /><button className="primary-button" type="submit">Сохранить робота</button></AdminActions>
        </form>)}
      </AdminSection>}

      {tab === "activities" && activityTab !== "hunting" && <AdminSection title={activityTab === "hacking" ? "Взлом" : "Контракты"}>
        <AdminRecordList rows={visibleContracts.map((contract) => ({ id: contract.id, title: contract.name, detail: contract.isActive ? "Активен" : "Отключён" }))} selectedId={selectedContractId} onSelect={setSelectedContractId} createLabel="Новый контракт" onCreate={() => setSelectedContractId("new")} />
        {selectedContractId === "new" && <form className="admin-editor admin-form" key={`new-${activityTab}`} onSubmit={createContract}><h3>Новый контракт</h3><AdminGroup title="Основное" wide><label>Название<input name="name" maxLength={120} required /></label><label>Описание<textarea name="description" maxLength={500} required /></label></AdminGroup><AdminGroup title="Условия"><ContractProgressionFields direction={activityTab === "hacking" ? "hacking" : "normal"} /><NumberField name="costNerve" label="Стоимость" value={1} min={1} /><NumberField name="successChance" label="Базовый шанс (0–1)" value={1} min={0} max={1} step="0.01" /></AdminGroup><AdminGroup title="Награды"><NumberField name="rewardCredits" label="Награда" value={0} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardExperience" label="Общий опыт" value={0} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardHackingExperience" label="Опыт Взлома" value={0} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked /></AdminGroup><AdminActions><ReasonField /><button className="primary-button" type="submit">Создать контракт</button></AdminActions></form>}
        {selectedContract && <form className="admin-editor admin-form" key={selectedContract.id} onSubmit={(event) => { event.preventDefault(); void mutate(`/api/admin/contracts/${selectedContract.id}`, contractPayload(new FormData(event.currentTarget))); }}>
          <h3>{selectedContract.name}</h3><AdminGroup title="Основное" wide><label>Название<input name="name" defaultValue={selectedContract.name} maxLength={120} required /></label><label>Описание<textarea name="description" defaultValue={selectedContract.description} maxLength={500} required /></label></AdminGroup><AdminGroup title="Условия"><ContractProgressionFields contract={selectedContract} /><NumberField name="costNerve" label="Стоимость" value={selectedContract.costNerve} min={1} /><NumberField name="successChance" label="Базовый шанс (0–1)" value={selectedContract.successChance} min={0} max={1} step="0.01" /></AdminGroup><AdminGroup title="Награды"><NumberField name="rewardCredits" label="Награда" value={selectedContract.rewardCredits} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardExperience" label="Общий опыт" value={selectedContract.rewardExperience} min={0} max={MAX_ECONOMY_VALUE} /><NumberField name="rewardHackingExperience" label="Опыт Взлома" value={selectedContract.rewardHackingExperience} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked={selectedContract.isActive} /></AdminGroup><AdminActions><ReasonField /><button className="primary-button" type="submit">Сохранить контракт</button></AdminActions>
        </form>}
      </AdminSection>}

      {tab === "items" && <AdminSection title="Предметы">
        <div className="admin-item-tools">
          <nav className="admin-subtabs" aria-label="Типы предметов">
            {ITEM_CATEGORIES.map(([value, label]) => <button key={value} type="button" className={itemCategory === value ? "selected" : ""} aria-current={itemCategory === value ? "page" : undefined} onClick={() => { setItemCategory(value); setSelectedItemId((current) => current === "new" ? current : null); }}>{label}</button>)}
          </nav>
          <div className="admin-item-search"><label htmlFor="admin-item-search">Поиск по названию</label><input id="admin-item-search" type="search" value={itemSearch} onChange={(event) => { setItemSearch(event.target.value); setSelectedItemId((current) => current === "new" ? current : null); }} placeholder="Например, карабин" /><button type="button" className="primary-button" onClick={() => setSelectedItemId("new")}>＋ Новый предмет</button></div>
        </div>
        <div className="admin-item-results">
          {visibleItems.length > 0 ? <AdminRecordList rows={visibleItems.map((item) => ({ id: item.id, title: item.name, detail: `${ITEM_TYPE_LABELS[item.type] ?? item.type} · ${item.isActive ? "активен" : "отключён"}` }))} selectedId={selectedItemId} onSelect={setSelectedItemId} /> : <div className="admin-item-empty"><p>{itemSearch.trim() ? "По вашему запросу предметы не найдены." : "В этой категории пока нет предметов."}</p>{itemSearch.trim() && <button type="button" className="secondary-button" onClick={() => setItemSearch("")}>Сбросить поиск</button>}</div>}
        </div>
        {selectedItemId === "new" && <form className="admin-editor admin-form" key={`new-item-${itemCategory}`} onSubmit={createItem}><h3>Новый предмет</h3><AdminGroup title="Основное" wide><label>Название<input name="name" maxLength={120} required /></label><label>Описание<textarea name="description" maxLength={500} required /></label></AdminGroup><AdminGroup title="Тип и доступность"><label>Тип<select name="type" defaultValue={itemCategory === "all" ? "weapon" : itemCategory}><option value="consumable">Расходник</option><option value="weapon">Оружие</option><option value="armor">Броня</option><option value="ammunition">Боеприпасы</option><option value="material">Материал</option></select></label><label>Слот<select name="allowedSlots" defaultValue={itemCategory === "armor" ? "helmet" : itemCategory === "all" || itemCategory === "weapon" ? "primary" : ""}><option value="primary">Основное</option><option value="secondary">Вторичное</option><option value="melee">Ближний бой</option><option value="helmet">Голова</option><option value="body">Корпус</option><option value="pants">Ноги</option><option value="gloves">Перчатки</option><option value="boots">Обувь</option><option value="">Нет</option></select></label><NumberField name="price" label="Цена" value={0} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked /></AdminGroup><ItemDefinitionFields createType={itemCategory === "all" ? "weapon" : itemCategory} /><AdminActions><ReasonField /><button className="primary-button" type="submit">Создать предмет</button></AdminActions></form>}
        {selectedItem && <form className="admin-editor admin-form" key={selectedItem.id} onSubmit={(event) => { event.preventDefault(); void mutate(`/api/admin/items/${selectedItem.id}`, itemUpdatePayload(new FormData(event.currentTarget))); }}>
          <h3>{selectedItem.name}</h3><p>Тип: {ITEM_TYPE_LABELS[selectedItem.type] ?? selectedItem.type} · Слот: {selectedItem.allowedSlots || "нет"}. Тип и совместимость выданного предмета не меняются.</p><AdminGroup title="Основное" wide><label>Описание<textarea name="description" defaultValue={selectedItem.description} maxLength={500} required /></label></AdminGroup><AdminGroup title="Цена и доступность"><NumberField name="price" label="Цена" value={selectedItem.price} min={0} max={MAX_ECONOMY_VALUE} /><CheckField name="isActive" label="Активен" checked={selectedItem.isActive} /></AdminGroup><ItemDefinitionFields item={selectedItem} /><AdminActions><ReasonField /><button className="primary-button" type="submit">Сохранить предмет</button></AdminActions>
        </form>}
      </AdminSection>}

      {tab === "npcs" && <AdminSection title="НПЦ">
        <nav className="admin-subtabs" aria-label="Категории НПЦ">
          {([ ["peaceful", "Мирные"], ["robot", "Роботы"], ["mutant", "Мутанты"], ["animal", "Животные"] ] as const).map(([value, label]) =>
            <button key={value} type="button" className={npcCategory === value ? "selected" : ""} onClick={() => { setNpcCategory(value); setSelectedNpcId(null); }}>{label}</button>)}
        </nav>
        <AdminRecordList rows={npcs.filter((npc) => npc.category === npcCategory).map((npc) => ({ id: npc.id, title: npc.name, detail: `${npc.portraitPath ? "Портрет задан" : "Портрет: заглушка"} · ${npc.source === "hunting" ? "В охоте" : "Шаблон"} · ${npc.isActive ? "активен" : "отключён"}` }))} selectedId={selectedNpcId} onSelect={setSelectedNpcId} createLabel="Новый НПЦ" onCreate={() => setSelectedNpcId("new")} />
        {selectedNpcId === "new" && <NpcEditor key={`new-${npcCategory}`} category={npcCategory} items={items} onSubmit={(event) => void saveNpc(event)} />}
        {selectedNpc && <NpcEditor key={selectedNpc.id} category={npcCategory} npc={selectedNpc} items={items} onSubmit={(event) => void saveNpc(event, selectedNpc.id)} />}
      </AdminSection>}

      {tab === "logs" && <AdminSection title="Журналы"><nav className="admin-subtabs" aria-label="Вид журнала"><button type="button" className={logTab === "actions" ? "selected" : ""} onClick={() => { setLogTab("actions"); setSelectedLogId(null); }}>Действия игроков</button><button type="button" className={logTab === "audits" ? "selected" : ""} onClick={() => { setLogTab("audits"); setSelectedLogId(null); }}>Изменения администратора</button></nav><AdminRecordList rows={visibleLogs.map((log) => ({ id: log.id, title: log.title, detail: new Date(log.date).toLocaleString("ru-RU") }))} selectedId={selectedLogId} onSelect={setSelectedLogId} />{selectedLog && <article className="admin-editor"><h3>{selectedLog.title}</h3><time>{new Date(selectedLog.date).toLocaleString("ru-RU")}</time>{selectedLog.comment && <p>Комментарий: {selectedLog.comment}</p>}{selectedLog.technical && <details><summary>Технические подробности</summary><pre>{JSON.stringify({ action: selectedLog.technical.action, targetType: selectedLog.technical.targetType, targetId: selectedLog.technical.targetId, oldValue: parseAuditValue(selectedLog.technical.oldValue), newValue: parseAuditValue(selectedLog.technical.newValue) }, null, 2)}</pre></details>}</article>}</AdminSection>}
    </section></main>
  );
}

function AdminSection({ title, children }: Readonly<{ title: string; children: React.ReactNode }>) { return <section className="admin-section"><h2>{title}</h2>{children}</section>; }
function AdminRecordList({ rows, selectedId, onSelect, createLabel, onCreate }: Readonly<{ rows: { id: string; title: string; detail: string }[]; selectedId: string | null; onSelect: (id: string) => void; createLabel?: string; onCreate?: () => void }>) { return <div className="admin-list">{onCreate && <button type="button" className={`admin-row admin-create${selectedId === "new" ? " selected" : ""}`} onClick={onCreate}>＋ {createLabel}</button>}{rows.map((row) => <button type="button" className={`admin-row${selectedId === row.id ? " selected" : ""}`} key={row.id} onClick={() => onSelect(row.id)}><span>{row.title}</span><small>{row.detail}</small></button>)}</div>; }
function AdminGroup({ title, children, wide = false }: Readonly<{ title: string; children: React.ReactNode; wide?: boolean }>) { return <fieldset className={`admin-group${wide ? " wide" : ""}`}><legend>{title}</legend><div className="admin-group-fields">{children}</div></fieldset>; }
function AdminActions({ children }: Readonly<{ children: React.ReactNode }>) { return <div className="admin-actions">{children}</div>; }
function NpcEditor({ category, npc, items, onSubmit }: Readonly<{ category: NpcCategory; npc?: Npc; items: Item[]; onSubmit: (event: FormEvent<HTMLFormElement>) => void }>) {
  const [currentCategory, setCurrentCategory] = useState<NpcCategory>(npc?.category ?? category);
  const requiredCombat = currentCategory !== "peaceful";
  const [lootRows, setLootRows] = useState<NpcLootRow[]>(npc?.lootRows ?? []);
  const [itemPicker, setItemPicker] = useState(false);
  const [itemSearch, setItemSearch] = useState("");
  const [zoneValues, setZoneValues] = useState<Record<string, NpcZone>>(() => Object.fromEntries(BODY_ZONES.map(([key]) =>
    [key, npc?.armorZones?.[key] ?? { coverage: 0, protection: 0 }])));
  const [allCoverage, setAllCoverage] = useState(0);
  const [allProtection, setAllProtection] = useState(0);
  const number = (name: "health" | "strength" | "toughness" | "reflexes" | "agility" | "weaponDamage" | "weaponAccuracy", label: string, min: number, max: number, step?: string) => <label>{label}<input name={name} type="number" defaultValue={npc?.[name] ?? ""} min={min} max={max} step={step} required={requiredCombat} /></label>;
  return <form className="admin-editor admin-form" onSubmit={onSubmit}>
    <h3>{npc ? npc.name : "Новый НПЦ"}</h3>
    {npc?.source === "hunting" && <p>Этот НПЦ настроен для охоты. Изменения имени, боевых параметров и активности действуют на будущие бои; награды и условия охоты редактируются в «Активности → Охота».</p>}
    <AdminGroup title="Основное" wide>
      <label>Название<input name="name" defaultValue={npc?.name ?? ""} maxLength={120} required /></label>
      <label>Описание<textarea name="description" defaultValue={npc?.description ?? ""} maxLength={500} required /></label>
      {npc?.source === "hunting" ? <input type="hidden" name="category" value={npc.category} /> : <label>Категория<select name="category" value={currentCategory} onChange={(event) => setCurrentCategory(event.target.value as NpcCategory)}><option value="peaceful">Мирные</option><option value="robot">Роботы</option><option value="mutant">Мутанты</option><option value="animal">Животные</option></select></label>}
      <CheckField name="isActive" label="Активен" checked={npc?.isActive ?? true} />
    </AdminGroup>
    <AdminGroup title="Изображения" wide>
      <label>Портрет в списках (HTTPS или путь от /)<input name="portraitPath" defaultValue={npc?.portraitPath ?? ""} maxLength={500} placeholder="/images/npc/portrait.png" /></label>
      <label>Фигура в бою (HTTPS или путь от /)<input name="figurePath" defaultValue={npc?.figurePath ?? ""} maxLength={500} placeholder="/images/npc/figure.png" /></label>
      <p>Портрет: {npc?.portraitPath || "Нет изображения — будет заглушка"}. Фигура: {npc?.figurePath || "Нет изображения — будет заглушка"}.</p>
    </AdminGroup>
    <AdminGroup title={requiredCombat ? "Боевые характеристики" : "Боевые характеристики (необязательно)"}>
      {number("health", "Здоровье", 1, 1_000_000)}
      {number("strength", "Сила (STR)", 1, 1_000_000)}
      {number("toughness", "Стойкость (TGH)", 1, 1_000_000)}
      {number("reflexes", "Реакция (REF)", 1, 1_000_000)}
      {number("agility", "Ловкость (AGI)", 1, 1_000_000)}
      <label>Оружие / тип атаки<input name="weaponName" defaultValue={npc?.weaponName ?? ""} maxLength={120} required={requiredCombat} /></label>
      {number("weaponDamage", "Урон оружия", 1, 1000, "0.01")}
      {number("weaponAccuracy", "Точность", 0, 100, "0.01")}
    </AdminGroup>
    {(requiredCombat || npc?.health != null) && <AdminGroup title="Защита по зонам тела" wide>
      <p>Покрытие — шанс срабатывания; защита — снижение урона при срабатывании. При покрытии 0 защита не действует.</p>
      <div className="npc-zone-table"><div className="npc-table-head"><span>Зона тела</span><span>Покрытие, %</span><span>Защита, %</span></div>
        {BODY_ZONES.map(([key, label]) => <div className="npc-zone-row" key={key}><strong>{label}</strong>
          <input aria-label={`${label}: покрытие, %`} name={`zoneCoverage-${key}`} type="number" min={0} max={100} step="0.01" required value={zoneValues[key].coverage} onChange={(event) => setZoneValues({ ...zoneValues, [key]: { ...zoneValues[key], coverage: Number(event.target.value) } })} />
          <input aria-label={`${label}: защита, %`} name={`zoneProtection-${key}`} type="number" min={0} max={100} step="0.01" required value={zoneValues[key].protection} onChange={(event) => setZoneValues({ ...zoneValues, [key]: { ...zoneValues[key], protection: Number(event.target.value) } })} /></div>)}
      </div>
      <div className="npc-apply-all"><label>Покрытие, %<input aria-label="Покрытие для всех зон" type="number" min={0} max={100} step="0.01" value={allCoverage} onChange={(event) => setAllCoverage(Number(event.target.value))} /></label>
        <label>Защита, %<input aria-label="Защита для всех зон" type="number" min={0} max={100} step="0.01" value={allProtection} onChange={(event) => setAllProtection(Number(event.target.value))} /></label>
        <button type="button" className="secondary-button" onClick={() => setZoneValues(Object.fromEntries(BODY_ZONES.map(([key]) => [key, { coverage: allCoverage, protection: allProtection }] )))}>Применить ко всем зонам</button></div>
    </AdminGroup>}
    {(requiredCombat || npc?.health != null) && <AdminGroup title="Добыча" wide>
      <p>Каждая строка разыгрывается отдельно. Сумма шансов может быть больше 100%. Удаление строки не удаляет предмет.</p>
      <div className="npc-loot-table"><div className="npc-table-head"><span>Предмет</span><span>Шанс выпадения, %</span><span>Количество от</span><span>Количество до</span><span>Убрать</span></div>
        {lootRows.map((row, index) => <div className="npc-loot-row" key={row.itemId}>
          <strong>{items.find((item) => item.id === row.itemId)?.name ?? row.itemName}</strong><input type="hidden" name={`lootItem-${index}`} value={row.itemId} />
          <input aria-label={`${row.itemName}: шанс выпадения, %`} name={`lootChance-${index}`} type="text" inputMode="decimal" defaultValue={row.chancePercent.toString().replace(".", ",")} pattern="(?:100(?:[,.]0{1,3})?|\d{1,2}(?:[,.]\d{1,3})?)" required />
          <input aria-label={`${row.itemName}: количество от`} name={`lootMin-${index}`} type="number" min={1} max={100} step={1} defaultValue={row.minimumQuantity} required />
          <input aria-label={`${row.itemName}: количество до`} name={`lootMax-${index}`} type="number" min={1} max={100} step={1} defaultValue={row.maximumQuantity} required />
          <button type="button" className="secondary-button" onClick={() => setLootRows(lootRows.filter((value) => value.itemId !== row.itemId))}>Убрать</button></div>)}
      </div>
      <input type="hidden" name="lootCount" value={lootRows.length} />
      <button type="button" className="secondary-button" onClick={() => setItemPicker(!itemPicker)}>Добавить предмет</button>
      {itemPicker && <div className="npc-item-picker"><label>Поиск предмета<input value={itemSearch} onChange={(event) => setItemSearch(event.target.value)} placeholder="Название предмета" /></label><div className="npc-item-results">{items.filter((item) => item.name.toLocaleLowerCase("ru").includes(itemSearch.toLocaleLowerCase("ru")) && !lootRows.some((row) => row.itemId === item.id)).map((item) => <button type="button" key={item.id} onClick={() => { setLootRows([...lootRows, { itemId: item.id, itemName: item.name, chancePercent: 0, minimumQuantity: 1, maximumQuantity: 1 }]); setItemPicker(false); setItemSearch(""); }}>{item.name}</button>)}</div></div>}
    </AdminGroup>}
    <AdminActions><ReasonField /><button className="primary-button" type="submit">{npc ? "Сохранить НПЦ" : "Создать НПЦ"}</button></AdminActions>
  </form>;
}
function npcPayload(form: FormData, fallbackCategory: NpcCategory) {
  const nullableNumber = (name: string) => { const raw = String(form.get(name) ?? "").trim(); return raw === "" ? null : Number(raw); };
  const nullableText = (name: string) => String(form.get(name) ?? "").trim() || null;
  const combat = String(form.get("zoneCoverage-head") ?? "") !== "";
  const armorZones = combat ? Object.fromEntries(BODY_ZONES.map(([key]) => [key, { coverage: Number(form.get(`zoneCoverage-${key}`)), protection: Number(form.get(`zoneProtection-${key}`)) }])) : null;
  const lootRows = Array.from({ length: Number(form.get("lootCount") ?? 0) }, (_, index) => {
    const rawChance = String(form.get(`lootChance-${index}`) ?? "").trim().replace(",", ".");
    if (!/^(?:100(?:\.0{1,3})?|\d{1,2}(?:\.\d{1,3})?)$/.test(rawChance)) throw new Error("Шанс выпадения: введите от 0 до 100% с точностью до 0,001%.");
    return { itemId: form.get(`lootItem-${index}`), chancePercent: Number(rawChance), minimumQuantity: Number(form.get(`lootMin-${index}`)), maximumQuantity: Number(form.get(`lootMax-${index}`)) };
  });
  return { category: form.get("category") ?? fallbackCategory, name: form.get("name"), description: form.get("description"),
    isActive: form.get("isActive") === "on", portraitPath: nullableText("portraitPath"), figurePath: nullableText("figurePath"),
    health: nullableNumber("health"), strength: nullableNumber("strength"), toughness: nullableNumber("toughness"),
    reflexes: nullableNumber("reflexes"), agility: nullableNumber("agility"), weaponName: nullableText("weaponName"),
    weaponDamage: nullableNumber("weaponDamage"), weaponAccuracy: nullableNumber("weaponAccuracy"),
    armor: combat ? 0 : null, armorCoverage: combat ? "{}" : null, armorZones, lootRows, reason: form.get("reason") };
}
function NumberField({ name, label, value, min, max, step }: Readonly<{ name: string; label: string; value: number; min?: number; max?: number; step?: string }>) { return <label>{label}<input name={name} type="number" defaultValue={value} min={min} max={max} step={step} required /></label>; }
function HuntingSetupFields() { const field = (name: string, label: string, min: number, max: number) =>
  <label key={name}>{label}<input name={name} type="number" min={min} max={max} step="1" required /></label>;
  return <AdminGroup title="Параметры охоты для нового НПЦ — задаются один раз">
    <p>Боевые характеристики берутся из карточки НПЦ. Укажите стоимость, тип атаки (для ближнего боя магазин, очередь и запас — 0) и награды. Автоматического баланса нет.</p>
    {field("minimumHuntingLevel", "Минимальный уровень охоты", 1, 20)}
    {field("energyCost", "Стоимость энергии", 1, 1000000)}
    {field("clipSize", "Патронов в магазине", 0, 1000)}
    {field("rateOfFireMin", "Очередь от", 0, 1000)}
    {field("rateOfFireMax", "Очередь до", 0, 1000)}
    {field("spareClips", "Запасных магазинов", 0, 3)}
    {field("rewardCredits", "Базовые кредиты", 0, MAX_ECONOMY_VALUE)}
    {field("minimumCredits", "Дополнительные кредиты от", 0, MAX_ECONOMY_VALUE)}
    {field("maximumCredits", "Дополнительные кредиты до", 0, MAX_ECONOMY_VALUE)}
    {field("rewardExperience", "Общий опыт", 0, MAX_ECONOMY_VALUE)}
    {field("rewardHuntingExperience", "Опыт охоты", 0, MAX_ECONOMY_VALUE)}
  </AdminGroup>;
}
function huntingSetupPayload(form: FormData) { const value = (name: string) => Number(form.get(name)); return {
  minimumHuntingLevel: value("minimumHuntingLevel"), energyCost: value("energyCost"),
  clipSize: value("clipSize"), rateOfFireMin: value("rateOfFireMin"), rateOfFireMax: value("rateOfFireMax"),
  spareClips: value("spareClips"), rewardCredits: value("rewardCredits"),
  minimumCredits: value("minimumCredits"), maximumCredits: value("maximumCredits"),
  rewardExperience: value("rewardExperience"), rewardHuntingExperience: value("rewardHuntingExperience") };
}
function ReasonField() { return <label className="reason-field">Причина<input name="reason" minLength={3} maxLength={500} required /></label>; }
function CommentField() { return <label className="comment-field">Комментарий (необязательно)<input name="comment" maxLength={500} /></label>; }
function npcCategoryLabel(category: string) { return ({ robot: "Робот", mutant: "Мутант", animal: "Животное", peaceful: "Мирный" } as Record<string, string>)[category] ?? "НПЦ"; }
function parseAuditValue(value: string): Record<string, unknown> {
  try { const parsed: unknown = JSON.parse(value); return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? parsed as Record<string, unknown> : {}; }
  catch { return {}; }
}
function auditText(value: Record<string, unknown>, key: string): string | null {
  return typeof value[key] === "string" ? value[key] as string : null;
}
function formatAdminAudit(log: AuditLog, locations: HuntingLocation[], npcs: Npc[]): DisplayLog {
  const before = parseAuditValue(log.oldValue);
  const after = parseAuditValue(log.newValue);
  if (log.action === "hunting_npc_assign" || log.action === "hunting_npc_remove") {
    const locationId = auditText(after, "locationId") ?? auditText(before, "locationId");
    const npcId = auditText(after, "npcId") ?? auditText(before, "npcId");
    const locationName = auditText(after, "locationName") ?? auditText(before, "locationName") ?? locations.find((value) => value.id === locationId)?.name ?? "локацию";
    const npcName = auditText(after, "npcName") ?? auditText(before, "npcName") ?? npcs.find((value) => value.id === npcId)?.name ?? "НПЦ";
    const verb = log.action === "hunting_npc_assign" ? "добавил" : "убрал";
    const preposition = log.action === "hunting_npc_assign" ? "в" : "из";
    const comment = auditText(after, "comment") ?? (auditText(after, "locationName") ? null : log.reason);
    return { id: log.id, date: log.createdAtUtc,
      title: `${log.adminUsername} ${verb} «${npcName}» ${preposition} «${locationName}»`, comment, technical: log };
  }
  const labels: Record<string, string> = { admin_created: "создал администратора", player_block: "изменил блокировку игрока",
    resources_update: "изменил ресурсы игрока", contract_create: "создал контракт", contract_update: "изменил контракт",
    item_create: "создал предмет", item_update: "изменил предмет", hunting_location_update: "изменил локацию охоты",
    hunting_target_update: "изменил цель охоты", npc_create: "создал НПЦ", npc_update: "изменил НПЦ",
    npc_hunting_robot_update: "изменил боевого НПЦ", npc_hunting_setup: "настроил охоту для НПЦ" };
  return { id: log.id, date: log.createdAtUtc, title: `${log.adminUsername} ${labels[log.action] ?? "изменил данные"}`,
    comment: "comment" in after ? auditText(after, "comment") : log.reason, technical: log };
}
function CheckField({ name, label, checked }: Readonly<{ name: string; label: string; checked: boolean }>) { return <label className="check-field"><input name={name} type="checkbox" defaultChecked={checked} />{label}</label>; }
function ContractProgressionFields({ contract, direction = "normal" }: Readonly<{ contract?: Contract; direction?: "normal" | "hacking" }>) { return <><label>Направление<select name="direction" defaultValue={contract?.direction ?? direction}><option value="normal">Обычный</option><option value="hacking">Взлом</option></select></label><NumberField name="minimumLevel" label="Минимальный общий уровень" value={contract?.minimumLevel ?? 1} min={1} max={20} /><NumberField name="minimumHackingLevel" label="Минимальный Взлом" value={contract?.minimumHackingLevel ?? 1} min={1} max={20} /></>; }
function contractPayload(form: FormData) { return { name: form.get("name"), description: form.get("description"), direction: form.get("direction"), minimumLevel: Number(form.get("minimumLevel")), minimumHackingLevel: Number(form.get("minimumHackingLevel")), costNerve: Number(form.get("costNerve")), successChance: Number(form.get("successChance")), rewardCredits: Number(form.get("rewardCredits")), rewardExperience: Number(form.get("rewardExperience")), rewardHackingExperience: Number(form.get("rewardHackingExperience")), isActive: form.get("isActive") === "on", reason: form.get("reason") }; }
function ItemDefinitionFields({ item, createType = "weapon" }: Readonly<{ item?: Item; createType?: ItemCategory }>) { return <>
  <AdminGroup title="Расходник"><label>Ресурс расходника<select name="effectResource" defaultValue={item?.effectResource ?? (createType === "consumable" ? "energy" : "")}><option value="energy">Энергия</option><option value="nerve">Хладнокровие</option><option value="health">Здоровье</option><option value="">Нет</option></select></label><NumberField name="effectAmount" label="Сила расходника" value={item?.effectAmount ?? 0} min={0} /><NumberField name="cooldownSeconds" label="Ожидание расходника (секунды, 0 — нет)" value={item?.cooldownSeconds ?? 0} min={0} max={86400} /><label>Группа ожидания (латиница, цифры, _ и -)<input name="cooldownGroup" defaultValue={item?.cooldownGroup ?? ""} maxLength={64} /></label></AdminGroup>
  <AdminGroup title="Оружие"><NumberField name="minimumDamage" label="Урон от" value={item?.minimumDamage ?? 0} min={0} max={1000} step="0.01" /><NumberField name="maximumDamage" label="Урон до" value={item?.maximumDamage ?? 0} min={0} max={1000} step="0.01" /><NumberField name="minimumAccuracy" label="Точность от" value={item?.minimumAccuracy ?? 0} min={0} max={100} step="0.01" /><NumberField name="maximumAccuracy" label="Точность до" value={item?.maximumAccuracy ?? 0} min={0} max={100} step="0.01" /><label>Калибр<input name="caliber" defaultValue={item?.caliber ?? ""} maxLength={40} /></label><NumberField name="clipSize" label="Магазин" value={item?.clipSize ?? 0} min={0} max={1000} /><NumberField name="rateOfFireMin" label="RoF от" value={item?.rateOfFireMin ?? 0} min={0} max={1000} /><NumberField name="rateOfFireMax" label="RoF до" value={item?.rateOfFireMax ?? 0} min={0} max={1000} /></AdminGroup>
  <AdminGroup title="Броня"><NumberField name="minimumArmor" label="Броня от" value={item?.minimumArmor ?? 0} min={0} max={100} step="0.01" /><NumberField name="maximumArmor" label="Броня до" value={item?.maximumArmor ?? 0} min={0} max={100} step="0.01" /><label>Покрытие брони (JSON)<textarea name="armorCoverage" defaultValue={item?.armorCoverage ?? "{}"} required /></label></AdminGroup>
</>; }
function itemUpdatePayload(form: FormData) { return { price: Number(form.get("price")), description: form.get("description"), effectResource: form.get("effectResource"), effectAmount: Number(form.get("effectAmount")), cooldownSeconds: Number(form.get("cooldownSeconds")), cooldownGroup: form.get("cooldownGroup"), minimumDamage: Number(form.get("minimumDamage")), maximumDamage: Number(form.get("maximumDamage")), minimumAccuracy: Number(form.get("minimumAccuracy")), maximumAccuracy: Number(form.get("maximumAccuracy")), minimumArmor: Number(form.get("minimumArmor")), maximumArmor: Number(form.get("maximumArmor")), armorCoverage: form.get("armorCoverage"), caliber: form.get("caliber"), clipSize: Number(form.get("clipSize")), rateOfFireMin: Number(form.get("rateOfFireMin")), rateOfFireMax: Number(form.get("rateOfFireMax")), isActive: form.get("isActive") === "on", reason: form.get("reason") }; }
function itemCreatePayload(form: FormData) { return { name: form.get("name"), type: form.get("type"), allowedSlots: form.get("allowedSlots"), ...itemUpdatePayload(form) }; }
function huntingTargetPayload(form: FormData, target: HuntingTarget) { const number = (name: string) => Number(form.get(name)); return { name: form.get("name"), description: form.get("description"), minimumHuntingLevel: number("minimumHuntingLevel"), energyCost: number("energyCost"), health: number("health"), strength: number("strength"), reflexes: number("reflexes"), toughness: number("toughness"), agility: number("agility"), weaponName: form.get("weaponName"), weaponDamage: number("weaponDamage"), weaponAccuracy: number("weaponAccuracy"), clipSize: number("clipSize"), rateOfFireMin: number("rateOfFireMin"), rateOfFireMax: number("rateOfFireMax"), spareClips: number("spareClips"), armor: target.armor, armorCoverage: target.armorCoverage, rewardCredits: number("rewardCredits"), minimumCredits: number("minimumCredits"), maximumCredits: number("maximumCredits"), lootRows: null, rewardExperience: number("rewardExperience"), rewardHuntingExperience: number("rewardHuntingExperience"), isActive: form.get("isActive") === "on", reason: form.get("reason") }; }
