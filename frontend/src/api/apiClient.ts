const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5111/api';
export const TOKEN_KEY = 'ooad_jwt_token';

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setAuthToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token);
  } else {
    localStorage.removeItem(TOKEN_KEY);
  }
}

export function isApiNetworkError(err: unknown): boolean {
  if (err instanceof TypeError) return true;
  if (err instanceof Error) {
    const msg = err.message.toLowerCase();
    return (
      msg.includes('failed to fetch') ||
      msg.includes('networkerror') ||
      msg.includes('load failed') ||
      msg.includes('err_connection_refused') ||
      msg.includes('network request failed')
    );
  }
  return false;
}

export async function request<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;
  const token = getAuthToken();

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...((options?.headers as Record<string, string>) || {}),
  };

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorMessage = response.statusText;
    let errorType: string | undefined;

    try {
      const rawText = await response.text();
      if (rawText) {
        try {
          const errorData = JSON.parse(rawText) as { message?: string; title?: string; type?: string };
          errorType = errorData?.type;
          if (errorData?.message) {
            errorMessage = errorData.message;
          } else if (errorData?.title) {
            errorMessage = errorData.title;
          } else {
            errorMessage = rawText;
          }
        } catch {
          errorMessage = rawText;
        }
      }
    } catch {
      /* ignore body read failure */
    }

    if (response.status === 401 || (response.status === 403 && errorType === 'AccountLocked')) {
      setAuthToken(null);
    }

    throw new Error(errorMessage || `API Error [${response.status}]`);
  }

  if (response.status === 204) {
    return {} as T;
  }

  return response.json() as Promise<T>;
}
