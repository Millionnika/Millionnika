export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

export type ValidationProblem = {
  errors?: Record<string, string[]>;
  message?: string;
};

export async function getErrorMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as ValidationProblem;
    const messages = Object.values(problem.errors ?? {}).flat();
    return messages[0] ?? problem.message ?? "Не удалось выполнить запрос.";
  } catch {
    return "Сервер временно недоступен.";
  }
}
