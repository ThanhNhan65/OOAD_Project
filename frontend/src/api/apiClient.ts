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

  // Đọc response body dưới dạng text đúng 1 lần duy nhất để tránh lỗi stream already read
  const responseText = await response.text();
  let data: any = null;
  
  if (responseText) {
    try {
      data = JSON.parse(responseText);
    } catch {
      data = responseText;
    }
  }

  if (!response.ok) {
    let errorMessage = response.statusText;
    
    if (data) {
      if (typeof data === 'object' && data.message) {
        errorMessage = data.message;
      } else if (typeof data === 'string') {
        errorMessage = data;
      }
    }

    if (response.status === 401) {
      setAuthToken(null);
    }

    throw new Error(errorMessage || `API Error [${response.status}]`);
  }

  if (response.status === 204 || !responseText) {
    return {} as T;
  }

  return data as T;
}
