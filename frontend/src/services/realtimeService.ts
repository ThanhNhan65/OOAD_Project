import * as signalR from '@microsoft/signalr';
import { getAuthToken } from '../api/apiClient';
import { emitNavCountsChanged, emitNotificationsChanged } from '../api/notificationsApi';

let connection: signalR.HubConnection | null = null;
let toastHandler: ((title: string, content?: string) => void) | null = null;

function resolveHubUrl(): string {
  const apiUrl = import.meta.env.VITE_API_URL as string | undefined;
  if (apiUrl) {
    try {
      const parsed = new URL(apiUrl);
      return `${parsed.origin}/hubs/notifications`;
    } catch {
      // fallback below
    }
  }
  return import.meta.env.DEV
    ? '/hubs/notifications'
    : 'http://localhost:5111/hubs/notifications';
}

export async function startRealtimeConnection(
  onNotificationToast?: (title: string, content?: string) => void,
): Promise<void> {
  toastHandler = onNotificationToast ?? null;
  const token = getAuthToken();
  if (!token) return;

  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    return;
  }

  const conn = new signalR.HubConnectionBuilder()
    .withUrl(resolveHubUrl(), {
      accessTokenFactory: () => getAuthToken() ?? '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  conn.on('NotificationsUpdated', (payload?: { title?: string; content?: string } | null) => {
    emitNotificationsChanged();
    if (payload?.title && toastHandler) {
      toastHandler(payload.title, payload.content);
    }
  });

  conn.on('NavCountsUpdated', () => {
    emitNavCountsChanged();
  });

  conn.onreconnected(() => {
    emitNotificationsChanged();
    emitNavCountsChanged();
  });

  connection = conn;

  try {
    await conn.start();
  } catch {
    // Nếu mất kết nối tạm thời, automaticReconnect hoặc polling dự phòng vẫn hoạt động
  }
}

export async function stopRealtimeConnection(): Promise<void> {
  toastHandler = null;
  if (!connection) return;
  const current = connection;
  connection = null;
  try {
    await current.stop();
  } catch {
    // ignore stop errors
  }
}
