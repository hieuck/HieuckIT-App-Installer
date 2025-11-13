# HieuckIT App Installer (Tiếng Việt)

Một tiện ích Windows đơn giản được thiết kế để hợp lý hóa quá trình cài đặt các ứng dụng yêu thích của bạn. Cấu hình một lần với tệp YAML đơn giản và để trình cài đặt tự động xử lý quá trình tải xuống và cài đặt một cách im lặng.

## Tính năng

- **Điều khiển bằng YAML:** Dễ dàng xác định các ứng dụng bạn muốn cài đặt bằng tệp `apps.yaml` gọn gàng, dễ đọc.
- **Cài đặt Tự động:** Ứng dụng tự động tải xuống các trình cài đặt và chạy chúng với các tham số im lặng để bạn không cần can thiệp.
- **Hỗ trợ Patch:** Bao gồm chức năng áp dụng các bản vá hoặc tệp cấu hình bổ sung sau khi cài đặt.
- **Cấu hình Linh hoạt:** Ứng dụng có thể sử dụng tệp `apps.yaml` cục bộ hoặc lấy phiên bản mới nhất từ một kho lưu trữ trực tuyến.
- **Portable (Không cần cài đặt):** Bản thân trình cài đặt không cần cài đặt. Chỉ cần chạy tệp thực thi.
- **Tương thích Kiến trúc:** Tự động chọn phiên bản trình cài đặt chính xác (x86 hoặc x64) cho hệ thống của bạn.
- **Sẵn sàng cho CI/CD:** Đi kèm với một quy trình GitHub Actions để tự động xây dựng và phát hành ứng dụng.

## Cách Hoạt động

1.  **Tải Cấu hình:** Khi khởi chạy, trình cài đặt trước tiên sẽ tìm tệp `apps.yaml` trong thư mục của nó.
2.  **Dự phòng Online:** Nếu không tìm thấy cấu hình cục bộ, nó sẽ cố gắng tải xuống tệp `apps.yaml` mới nhất từ một URL trực tuyến được xác định trước.
3.  **Hiển thị Ứng dụng:** Danh sách ứng dụng từ tệp YAML được phân tích và hiển thị trên giao diện người dùng.
4.  **Cài đặt:** Người dùng chọn các ứng dụng mong muốn và nhấp vào "Install". Tiện ích sau đó sẽ tải xuống các tệp cần thiết và thực hiện các quy trình cài đặt và vá lỗi một cách im lặng trong nền.

## Hướng dẫn Sử dụng

1.  Tải xuống bản phát hành mới nhất từ tab [GitHub Actions](https://github.com/hieuck/HieuckIT-App-Installer/actions).
2.  Giải nén tệp `HieuckIT-App-Installer-Release.zip`.
3.  Chạy tệp `HieuckIT-App-Installer.exe`.
4.  Chọn các ứng dụng bạn muốn cài đặt từ danh sách.
5.  Nhấp vào nút **Install** và đợi quá trình hoàn tất.

## Cấu hình (`apps.yaml`)

Hành vi của ứng dụng được điều khiển bởi tệp `apps.yaml`, chứa danh sách các ứng dụng. Mỗi ứng dụng là một đối tượng với các thuộc tính sau:

```yaml
applications:
  - Name: "Ví dụ Ứng dụng"
    ProcessName: "example.exe"
    RegistryDisplayName: "Example App"
    InstallerArgs: "/S"
    DownloadLinks:
      - Name: "Trình cài đặt"
        Url_x64: "https://example.com/installer_64.exe"
        Url_x86: "https://example.com/installer_32.exe"
    IsArchive: false
    PatchArgs: 'x -y "{patch_path}" -o"{install_dir}"'
    PatchLinks:
      - Name: "Bản vá"
        Url_x64: "https://example.com/patch_64.rar"
        Url_x86: "https://example.com/patch_32.rar"

```
- **Name:** Tên hiển thị của ứng dụng trong giao diện.
- **ProcessName:** Tên tiến trình thực thi để kiểm tra xem ứng dụng có đang chạy hay không.
- **RegistryDisplayName:** Tên được tìm thấy trong Windows Registry để xác minh ứng dụng đã được cài đặt hay chưa.
- **InstallerArgs:** Các tham số dòng lệnh để cài đặt im lặng (ví dụ: `/S`, `/VERYSILENT`).
- **DownloadLinks:** Một danh sách chứa các liên kết tải xuống cho trình cài đặt. `Url_x64` và `Url_x86` chỉ định các liên kết cho hệ thống 64-bit và 32-bit.
- **IsArchive:** Đặt thành `true` nếu tệp tải về là một kho lưu trữ zip/rar cần giải nén thay vì một trình cài đặt chuẩn.
- **PatchArgs:** Tham số dòng lệnh cho công cụ vá lỗi (7-Zip) để áp dụng các bản vá.
- **PatchLinks:** Danh sách các liên kết tải xuống cho các tệp vá.

---

## Ủng hộ tôi (Support Me)

- **Vietcombank:** `9966595263`
- **MoMo:** `0966595263`
- **Chủ tài khoản:** LE TRUNG HIEU
- **PayPal:** [https://www.paypal.me/hieuck](https://www.paypal.me/hieuck)
