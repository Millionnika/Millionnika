"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { apiFetch, API_URL, getErrorMessage, PLAYER_STATE_EVENT, type PlayerStateEventDetail } from "@/lib/api";

type Resource = { current: number; maximum: number; nextRecoveryAtUtc: string | null };
type ProfileSummary = { username: string; role: string; level: number; credits: number; energy: Resource; nerve: Resource; health: Resource };
type FightSummary = { status: string; playerHealth: number; maximumPlayerHealth: number };

const navigation = [
  { title: "Персонаж", links: [["/profile", "Профиль"], ["/inventory", "Инвентарь"], ["/implant-center", "Тренировка"]] },
  { title: "Город", links: [["/shop", "Магазин"], ["/contracts", "Контракты"], ["/activities", "Активности"], ["/hacking", "Взлом", true], ["/hunting", "Охота", true]] },
  { title: "Общение", links: [["/players", "Игроки"], ["/friends", "Друзья"]] },
] as const;

export function GameNav() {
  const pathname = usePathname();
  const router = useRouter();
  const [profile, setProfile] = useState<ProfileSummary | null>(null);
  const [fightHealth, setFightHealth] = useState<{ current: number; maximum: number } | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [error, setError] = useState("");

  const loadProfile = useCallback(async () => {
    try {
      const [profileResponse, fightResponse] = await Promise.all([
        fetch(`${API_URL}/api/profile`, { credentials: "include", cache: "no-store" }),
        fetch(`${API_URL}/api/game/hunting/fight`, { credentials: "include", cache: "no-store" }),
      ]);
      if (profileResponse.status === 401) { setProfile(null); router.replace("/login"); return; }
      if (!profileResponse.ok) throw new Error("profile");
      setProfile(await profileResponse.json() as ProfileSummary);
      if (fightResponse.status === 204) setFightHealth(null);
      else if (fightResponse.ok) {
        const fight = await fightResponse.json() as FightSummary;
        setFightHealth(fight.status === "active" ? { current: fight.playerHealth, maximum: fight.maximumPlayerHealth } : null);
      }
      setError("");
    } catch { setError("Ресурсы временно недоступны"); }
  }, [router]);

  useEffect(() => { void loadProfile(); }, [loadProfile]);
  useEffect(() => { setMenuOpen(false); }, [pathname]);
  useEffect(() => {
    const handleUpdate = (event: Event) => {
      const detail = (event as CustomEvent<PlayerStateEventDetail>).detail;
      if (detail?.fightActive && detail.healthCurrent !== undefined && detail.healthMaximum !== undefined) {
        setFightHealth({ current: detail.healthCurrent, maximum: detail.healthMaximum });
      } else if (detail?.fightActive === false) setFightHealth(null);
      if (detail?.clear) { setProfile(null); setFightHealth(null); return; }
      void loadProfile();
    };
    window.addEventListener(PLAYER_STATE_EVENT, handleUpdate);
    return () => window.removeEventListener(PLAYER_STATE_EVENT, handleUpdate);
  }, [loadProfile]);

  const health = fightHealth ?? profile?.health;

  async function logout() {
    setError("");
    try {
      const response = await apiFetch(`${API_URL}/api/auth/logout`, { method: "POST", credentials: "include" });
      if (!response.ok && response.status !== 401) { setError(await getErrorMessage(response)); return; }
      setProfile(null); setFightHealth(null); router.replace("/login"); router.refresh();
    } catch { setError("Не удалось выйти. Повторите после восстановления связи."); }
  }

  return <>
    <header className="mobile-player-bar">
      <button className="mobile-menu-button" type="button" aria-expanded={menuOpen} aria-controls="game-sidebar" onClick={() => setMenuOpen((value) => !value)}>☰</button>
      <Link href="/profile" className="mobile-player-name">{profile?.username ?? "Миллионика"}</Link>
      <span className="mobile-resource">₡ {profile?.credits.toLocaleString("ru-RU") ?? "…"}</span>
      <span className="mobile-resource energy">Э {profile ? `${profile.energy.current}/${profile.energy.maximum}` : "…"}</span>
      <span className="mobile-resource health">З {health ? `${health.current}/${health.maximum}` : "…"}</span>
    </header>
    <aside id="game-sidebar" className={`game-sidebar${menuOpen ? " open" : ""}`} aria-label="Панель игрока">
      <div className="sidebar-brand">Миллионика</div>
      {profile ? <>
        <Link className="sidebar-player" href="/profile"><strong>{profile.username}</strong><span>Уровень {profile.level}</span></Link>
        <div className="sidebar-resources" aria-label="Ресурсы игрока">
          <CreditRow value={profile.credits} />
          <ResourceBar label="Энергия" resource={profile.energy} tone="energy" onElapsed={loadProfile} />
          <ResourceBar label="Хладнокровие" resource={profile.nerve} tone="nerve" onElapsed={loadProfile} />
          {health && <ResourceBar label="Здоровье" resource={{ ...health, nextRecoveryAtUtc: null }} tone="health" />}
        </div>
      </> : <p className="sidebar-loading">Загрузка ресурсов…</p>}
      {error && <p className="sidebar-error">{error}</p>}
      <nav className="sidebar-navigation" aria-label="Игровая навигация">
        {navigation.map((group) => <section key={group.title}>
          <h2>{group.title}</h2>
          {group.links.map(([href, label, nested]) => <Link className={`${isActive(pathname, href) ? "active" : ""}${nested ? " nested" : ""}`} href={href} key={href}>{label}</Link>)}
        </section>)}
        {profile?.role === "Admin" && <section><h2>Управление</h2><Link className={isActive(pathname, "/admin") ? "active" : ""} href="/admin">Админка</Link></section>}
      </nav>
      <button className="sidebar-logout" type="button" onClick={logout}>Выйти</button>
    </aside>
    {menuOpen && <button className="sidebar-scrim" type="button" aria-label="Закрыть меню" onClick={() => setMenuOpen(false)} />}
  </>;
}

function CreditRow({ value }: Readonly<{ value: number }>) { return <div className="credit-row"><span>Кредиты</span><strong>{value.toLocaleString("ru-RU")} ₡</strong></div>; }

function ResourceBar({ label, resource, tone, onElapsed }: Readonly<{ label: string; resource: Resource; tone: string; onElapsed?: () => void }>) {
  const percent = resource.maximum > 0 ? Math.min(100, Math.max(0, resource.current / resource.maximum * 100)) : 0;
  return <div className={`sidebar-resource ${tone}`}><div><span>{label}</span><strong>{resource.current}/{resource.maximum}</strong></div><div className="resource-track"><i style={{ width: `${percent}%` }} /></div>{resource.nextRecoveryAtUtc && <RecoveryCountdown targetUtc={resource.nextRecoveryAtUtc} onElapsed={onElapsed} />}</div>;
}

function RecoveryCountdown({ targetUtc, onElapsed }: Readonly<{ targetUtc: string; onElapsed?: () => void }>) {
  const [seconds, setSeconds] = useState(() => remaining(targetUtc));
  useEffect(() => {
    setSeconds(remaining(targetUtc));
    const timer = window.setInterval(() => { const value = remaining(targetUtc); setSeconds(value); if (value === 0) { window.clearInterval(timer); onElapsed?.(); } }, 1000);
    return () => window.clearInterval(timer);
  }, [targetUtc, onElapsed]);
  return <small>{Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, "0")}</small>;
}

function remaining(targetUtc: string) { return Math.max(0, Math.ceil((new Date(targetUtc).getTime() - Date.now()) / 1000)); }
function isActive(pathname: string, href: string) { return pathname === href || (href !== "/profile" && pathname.startsWith(`${href}/`)); }
