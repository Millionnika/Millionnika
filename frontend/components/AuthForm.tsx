"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { API_URL, getErrorMessage } from "@/lib/api";

type AuthMode = "register" | "login";
type AuthResponse = { username: string; role: string };

export function AuthForm({ mode }: Readonly<{ mode: AuthMode }>) {
  const router = useRouter();
  const isRegistration = mode === "register";
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setIsSubmitting(true);

    const form = new FormData(event.currentTarget);
    const body = isRegistration
      ? {
          username: form.get("username"),
          email: form.get("email"),
          password: form.get("password"),
        }
      : {
          login: form.get("login"),
          password: form.get("password"),
        };

    try {
      const response = await fetch(`${API_URL}/api/auth/${mode}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        setError(await getErrorMessage(response));
        return;
      }

      const result = (await response.json()) as AuthResponse;
      router.push(result.role === "Admin" ? "/admin" : "/profile");
      router.refresh();
    } catch {
      setError("Сервер игры недоступен.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main>
      <section className="auth-panel">
        <Link className="back-link" href="/">← На главную</Link>
        <p className="eyebrow">Миллионика</p>
        <h1 className="auth-title">{isRegistration ? "Создать персонажа" : "Вернуться в город"}</h1>

        <form onSubmit={handleSubmit} className="auth-form">
          {isRegistration ? (
            <>
              <label>
                Имя игрока
                <input
                  name="username"
                  autoComplete="username"
                  minLength={3}
                  maxLength={24}
                  required
                />
              </label>
              <label>
                Email
                <input name="email" type="email" autoComplete="email" maxLength={254} required />
              </label>
            </>
          ) : (
            <label>
              Имя игрока или email
              <input name="login" autoComplete="username" maxLength={254} required />
            </label>
          )}

          <label>
            Пароль
            <input
              name="password"
              type="password"
              autoComplete={isRegistration ? "new-password" : "current-password"}
              minLength={isRegistration ? 10 : 1}
              maxLength={128}
              required
            />
          </label>

          {isRegistration && (
            <p className="field-hint">Не менее 10 символов: заглавная и строчная буквы, цифра.</p>
          )}

          {error && <p className="form-error" role="alert">{error}</p>}

          <button className="primary-button" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Подождите…" : isRegistration ? "Зарегистрироваться" : "Войти"}
          </button>
        </form>

        <p className="auth-switch">
          {isRegistration ? "Уже есть персонаж?" : "Впервые в Миллионике?"}{" "}
          <Link href={isRegistration ? "/login" : "/register"}>
            {isRegistration ? "Войти" : "Зарегистрироваться"}
          </Link>
        </p>
      </section>
    </main>
  );
}
