"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";

type PublicProfile = {
  id: string; username: string; level: number; registeredAtUtc: string; description: string; canEdit: boolean;
};
type FriendStatus = { state: "none" | "outgoing" | "incoming" | "friends" | "self" | "unavailable"; relationId: string | null };

export default function PublicPlayerPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [profile, setProfile] = useState<PublicProfile | null>(null);
  const [description, setDescription] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [friendStatus, setFriendStatus] = useState<FriendStatus | null>(null);
  const [friendBusy, setFriendBusy] = useState(false);

  useEffect(() => {
    fetch(`${API_URL}/api/players/${id}`, { credentials: "include" }).then(async (response) => {
      if (response.status === 401) { router.replace("/login"); return; }
      if (response.status === 404) { setError("Игрок не найден или профиль недоступен."); return; }
      if (!response.ok) { setError(await getErrorMessage(response)); return; }
      const data = await response.json() as PublicProfile;
      setProfile(data); setDescription(data.description);
      if (!data.canEdit) {
        const statusResponse = await fetch(`${API_URL}/api/friends/status/${id}`, { credentials: "include" });
        if (statusResponse.ok) setFriendStatus(await statusResponse.json() as FriendStatus);
      }
    }).catch(() => setError("Сервер игры недоступен."));
  }, [id, router]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(""); setMessage("");
    const response = await fetch(`${API_URL}/api/players/${id}/description`, {
      method: "PUT", headers: { "Content-Type": "application/json" }, credentials: "include",
      body: JSON.stringify({ description }),
    });
    if (response.status === 401) { router.replace("/login"); return; }
    if (!response.ok) { setError(await getErrorMessage(response)); return; }
    const result = await response.json() as { message: string; description: string };
    setMessage(result.message);
    setDescription(result.description);
    setProfile((current) => current ? { ...current, description: result.description } : current);
  }

  async function addFriend() {
    setFriendBusy(true); setError(""); setMessage("");
    try {
      const response = await fetch(`${API_URL}/api/friends/requests`, {
        method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include",
        body: JSON.stringify({ playerId: id }),
      });
      const result = await response.json() as { message: string; state: FriendStatus["state"]; relationId: string | null };
      if (!response.ok) { setError(result.message); }
      else { setMessage(result.message); }
      setFriendStatus({ state: result.state, relationId: result.relationId });
    } catch { setError("Сервер игры недоступен."); }
    finally { setFriendBusy(false); }
  }

  return <main><section className="game-panel public-profile-panel">
    <Link className="back-link" href="/players">← Поиск игроков</Link>
    {error && !profile && <p className="form-error public-profile-error">{error}</p>}
    {profile && <>
      <p className="eyebrow game-eyebrow">Публичный профиль</p>
      <h1 className="game-title">{profile.username}</h1>
      <div className="public-profile-meta"><span>Уровень <strong>{profile.level}</strong></span><span>В городе с <strong>{new Date(profile.registeredAtUtc).toLocaleDateString("ru-RU")}</strong></span></div>
      <h2 className="section-title">О себе</h2>
      <p className="public-description">{profile.description || "Игрок пока ничего о себе не рассказал."}</p>
      {!profile.canEdit && friendStatus && <div className="friend-profile-action">
        {friendStatus.state === "none" && <button className="primary-button" type="button" disabled={friendBusy} onClick={addFriend}>Добавить в друзья</button>}
        {friendStatus.state === "outgoing" && <span>Заявка отправлена</span>}
        {friendStatus.state === "incoming" && <span>Есть входящая заявка</span>}
        {friendStatus.state === "friends" && <span>Уже в друзьях</span>}
        {friendStatus.state !== "none" && <Link className="secondary-button" href="/friends">Открыть друзей</Link>}
        {message && <p className="action-result success">{message}</p>}
        {error && <p className="form-error">{error}</p>}
      </div>}
      {profile.canEdit && <form className="description-form" onSubmit={save}>
        <label>Редактировать описание<textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={300} rows={6} /></label>
        <span>{description.length} / 300</span>
        {message && <p className="action-result success">{message}</p>}
        {error && <p className="form-error">{error}</p>}
        <button className="primary-button" type="submit">Сохранить</button>
      </form>}
    </>}
  </section></main>;
}
