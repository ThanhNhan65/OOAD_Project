import { useEffect, useId, useRef } from 'react';
import { Html5Qrcode } from 'html5-qrcode';

export interface QrCameraScannerProps {
  active: boolean;
  disabled?: boolean;
  onDecoded: (code: string) => void;
  onError?: (message: string) => void;
  onActiveChange?: (active: boolean) => void;
}

const SCAN_COOLDOWN_MS = 2500;

export function QrCameraScanner({
  active,
  disabled = false,
  onDecoded,
  onError,
  onActiveChange,
}: QrCameraScannerProps) {
  const rawId = useId();
  const elementId = `qr-reader-${rawId.replace(/[^a-zA-Z0-9_-]/g, '')}`;

  const onDecodedRef = useRef(onDecoded);
  const onErrorRef = useRef(onError);
  const onActiveChangeRef = useRef(onActiveChange);
  const disabledRef = useRef(disabled);
  const lastScanRef = useRef<{ code: string; at: number } | null>(null);
  const scannerRef = useRef<Html5Qrcode | null>(null);

  useEffect(() => {
    onDecodedRef.current = onDecoded;
  }, [onDecoded]);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  useEffect(() => {
    onActiveChangeRef.current = onActiveChange;
  }, [onActiveChange]);

  useEffect(() => {
    disabledRef.current = disabled;
  }, [disabled]);

  useEffect(() => {
    if (!active) return;

    if (typeof window === 'undefined' || !window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      onErrorRef.current?.('Trình duyệt không hỗ trợ hoặc cần chạy trên localhost/HTTPS để mở camera.');
      onActiveChangeRef.current?.(false);
      return;
    }

    let cancelled = false;
    const scanner = new Html5Qrcode(elementId);
    scannerRef.current = scanner;

    const handleSuccess = (decodedText: string) => {
      if (cancelled || disabledRef.current) return;
      const trimmed = decodedText.trim();
      if (!trimmed) return;

      const now = Date.now();
      const prev = lastScanRef.current;
      if (prev && prev.code === trimmed && now - prev.at < SCAN_COOLDOWN_MS) {
        return;
      }

      lastScanRef.current = { code: trimmed, at: now };
      onDecodedRef.current(trimmed);
    };

    const config = {
      fps: 10,
      qrbox: (viewfinderWidth: number, viewfinderHeight: number) => {
        const minEdge = Math.min(viewfinderWidth, viewfinderHeight);
        const size = Math.max(150, Math.min(240, Math.floor(minEdge * 0.68)));
        return { width: size, height: size };
      },
    };

    const startScanner = async () => {
      try {
        try {
          await scanner.start({ facingMode: 'environment' }, config, handleSuccess, () => {});
        } catch {
          const cameras = await Html5Qrcode.getCameras();
          if (cancelled) return;
          if (!cameras || cameras.length === 0) {
            throw new Error('Không tìm thấy thiết bị camera khả dụng.');
          }
          await scanner.start(cameras[0].id, config, handleSuccess, () => {});
        }

        if (cancelled) {
          if (scanner.isScanning) {
            await scanner.stop().catch(() => {});
          }
          try {
            scanner.clear();
          } catch {
            // ignore cleanup errors after aborted start
          }
        }
      } catch (err) {
        if (cancelled) return;
        const rawMsg = err instanceof Error ? err.message : String(err);
        const msg =
          rawMsg.toLowerCase().includes('permission') || rawMsg.toLowerCase().includes('notallowed')
            ? 'Không có quyền truy cập camera. Vui lòng cấp quyền camera cho trình duyệt.'
            : rawMsg || 'Không thể khởi động camera quét QR.';
        onErrorRef.current?.(msg);
        onActiveChangeRef.current?.(false);
      }
    };

    void startScanner();

    return () => {
      cancelled = true;
      const current = scannerRef.current;
      scannerRef.current = null;
      if (!current) return;

      const cleanup = async () => {
        try {
          if (current.isScanning) {
            await current.stop();
          }
        } catch {
          // ignore if camera is still starting or already stopped
        }
        try {
          current.clear();
        } catch {
          // ignore clear errors on unmount
        }
      };

      void cleanup();
    };
  }, [active, elementId]);

  return (
    <div className={`qr-camera-wrap${active ? '' : ' inactive'}`} aria-hidden={!active}>
      <div id={elementId} className="qr-camera-mount" />
    </div>
  );
}

export async function scanQrFromImageFile(file: File): Promise<string> {
  const tempId = `qr-file-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const tempEl = document.createElement('div');
  tempEl.id = tempId;
  tempEl.style.display = 'none';
  document.body.appendChild(tempEl);

  const scanner = new Html5Qrcode(tempId);
  try {
    return await scanner.scanFile(file, false);
  } finally {
    try {
      scanner.clear();
    } catch {
      // ignore cleanup errors
    }
    tempEl.remove();
  }
}
