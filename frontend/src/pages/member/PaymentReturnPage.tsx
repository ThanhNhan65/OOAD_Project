import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { CheckCircle2, Clock, Loader2, RefreshCw, ShieldCheck, XCircle } from 'lucide-react';
import { membershipPlansApi, type PaymentVerificationResultDto } from '../../api/membershipPlansApi';
import { emitAppDataChanged } from '../../api/notificationsApi';
import { Badge } from '../../components/ui';
import { fmtDateTime, fmtMoney } from '../../utils';

export function PaymentReturnPage() {
  const location = useLocation();
  const navigate = useNavigate();

  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const vnpTxnRef = params.get('vnp_TxnRef') || params.get('vnp_ResponseCode');
  const zaloTransId = params.get('apptransid');

  const [loading, setLoading] = useState(true);
  const [result, setResult] = useState<PaymentVerificationResultDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const verify = useCallback(async () => {
    if (!vnpTxnRef && !zaloTransId) {
      setError('Không tìm thấy tham số giao dịch từ cổng thanh toán VNPay hoặc ZaloPay.');
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const data = vnpTxnRef
        ? await membershipPlansApi.verifyVnpayReturn(location.search)
        : await membershipPlansApi.verifyZaloPayReturn(zaloTransId!);
      setResult(data);
      if (data.success) {
        emitAppDataChanged();
      }
    } catch (err) {
      setError((err as Error).message || 'Không thể xác thực giao dịch với máy chủ.');
    } finally {
      setLoading(false);
    }
  }, [location.search, vnpTxnRef, zaloTransId]);

  useEffect(() => {
    void verify();
  }, [verify]);

  const isSuccess = result?.success === true;
  const isPending = result?.status === 'Pending';

  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}>
      <div className="card" style={{ maxWidth: 560, width: '100%', padding: 28 }}>
        {loading ? (
          <div style={{ textAlign: 'center', padding: '32px 0' }}>
            <Loader2 size={42} color="#7c5cff" style={{ animation: 'spin 1s linear infinite', margin: '0 auto 16px' }} />
            <h2>Đang xác thực giao dịch với bên thứ 3...</h2>
            <p className="muted mt8">
              Hệ thống đang đối soát chữ ký bảo mật và trạng thái thanh toán từ {vnpTxnRef ? 'VNPay Sandbox' : 'ZaloPay Sandbox'}.
            </p>
          </div>
        ) : error ? (
          <div style={{ textAlign: 'center', padding: '20px 0' }}>
            <XCircle size={48} color="#ff5c8a" style={{ margin: '0 auto 12px' }} />
            <h2>Lỗi xác thực giao dịch</h2>
            <p className="muted mt8">{error}</p>
            <div className="row mt16" style={{ justifyContent: 'center', gap: 10 }}>
              <button className="btn" onClick={() => navigate('/member/memberships')}>
                Quay lại Gói tập
              </button>
              <button className="btn primary" onClick={verify}>
                <RefreshCw size={15} /> Thử kiểm tra lại
              </button>
            </div>
          </div>
        ) : result ? (
          <>
            <div style={{ textAlign: 'center', marginBottom: 20 }}>
              {isSuccess ? (
                <CheckCircle2 size={52} color="#2ed47a" style={{ margin: '0 auto 10px' }} />
              ) : isPending ? (
                <Clock size={52} color="#ffb020" style={{ margin: '0 auto 10px' }} />
              ) : (
                <XCircle size={52} color="#ff5c8a" style={{ margin: '0 auto 10px' }} />
              )}
              <h2>
                {isSuccess
                  ? 'Thanh toán đã được xác thực'
                  : isPending
                    ? 'Đang chờ xác nhận từ ZaloPay'
                    : 'Thanh toán không thành công'}
              </h2>
              <p className="muted mt8">{result.message}</p>
              <div className="mt8">
                <Badge color={isSuccess ? 'green' : isPending ? 'amber' : 'red'}>
                  {isSuccess ? 'Đã kích hoạt gói tập' : isPending ? 'Chờ thanh toán' : 'Giao dịch thất bại'}
                </Badge>
              </div>
            </div>

            <div className="card" style={{ background: 'rgba(255,255,255,0.03)', padding: 16 }}>
              <div className="row small mb16" style={{ gap: 8, color: '#2ed47a', alignItems: 'center' }}>
                <ShieldCheck size={16} />
                <b>Xác thực bên thứ 3: {result.verifiedBy}</b>
              </div>
              <div className="kv">
                <span>Cổng thanh toán</span>
                <b>{result.provider}</b>
              </div>
              {result.planName && (
                <div className="kv">
                  <span>Gói đăng ký</span>
                  <b>{result.planName}</b>
                </div>
              )}
              {result.amount > 0 && (
                <div className="kv">
                  <span>Số tiền</span>
                  <b>{fmtMoney(result.amount)}</b>
                </div>
              )}
              {result.transactionCode && (
                <div className="kv">
                  <span>Mã đơn hàng (Merchant)</span>
                  <span style={{ fontFamily: 'monospace' }}>{result.transactionCode}</span>
                </div>
              )}
              {result.gatewayTransactionNo && (
                <div className="kv">
                  <span>Mã giao dịch {result.provider}</span>
                  <span style={{ fontFamily: 'monospace' }}>{result.gatewayTransactionNo}</span>
                </div>
              )}
              {result.bankCode && (
                <div className="kv">
                  <span>Ngân hàng</span>
                  <b>{result.bankCode}</b>
                </div>
              )}
              {result.membershipStatus && (
                <div className="kv">
                  <span>Trạng thái gói tập</span>
                  <b>{result.membershipStatus}</b>
                </div>
              )}
              {result.paidAt && (
                <div className="kv">
                  <span>Thời gian xác nhận</span>
                  <span>{fmtDateTime(result.paidAt)}</span>
                </div>
              )}
            </div>

            <div className="row mt16" style={{ justifyContent: 'center', gap: 10 }}>
              {isPending && (
                <button className="btn" onClick={verify}>
                  <RefreshCw size={15} /> Kiểm tra lại trạng thái
                </button>
              )}
              <button className="btn" onClick={() => navigate('/member/payments')}>
                Lịch sử giao dịch
              </button>
              <button className="btn primary" onClick={() => navigate('/member/memberships')}>
                Về Gói tập của tôi
              </button>
            </div>
          </>
        ) : null}
      </div>
    </div>
  );
}
