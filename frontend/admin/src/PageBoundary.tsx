import { Component, Suspense, type ReactNode } from 'react';
/** Bao bọc trang tải bất đồng bộ và hiển thị hướng xử lý khi trang phát sinh lỗi. */
export default class PageBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  /** Chuyển ranh giới lỗi sang trạng thái dự phòng khi cây con ném lỗi render. */
  static getDerivedStateFromError() { return { failed: true }; }
  /** Hiển thị thông báo lỗi hoặc nội dung con với trạng thái chờ tải trang. */
  render() {
    if (this.state.failed) return <div className="session-loading" role="alert"><p>Không tải được trang. Kiểm tra kết nối và tải lại.</p><button onClick={() => window.location.reload()}>Tải lại trang</button></div>;
    return <Suspense fallback={<div className="session-loading" role="status">Đang tải trang…</div>}>{this.props.children}</Suspense>;
  }
}
