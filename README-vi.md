# HieuckIT App Installer

Một tiện ích Windows đơn giản được thiết kế để hợp lý hóa quá trình cài đặt các ứng dụng yêu thích của bạn. Chỉ cần cấu hình một lần với tệp YAML đơn giản và để trình cài đặt xử lý việc tải xuống và cài đặt im lặng cho bạn.

## Tính năng

- **Điều khiển bằng YAML:** Dễ dàng xác định các ứng dụng bạn muốn cài đặt bằng tệp `apps.yaml` sạch sẽ, dễ đọc.
- **Cài đặt tự động:** Ứng dụng tự động tải xuống trình cài đặt và chạy chúng với các đối số im lặng để thiết lập rảnh tay.
- **Hỗ trợ bản vá:** Bao gồm chức năng áp dụng các bản vá hoặc các tệp cấu hình bổ sung sau khi cài đặt.
- **Cấu hình linh hoạt:** Ứng dụng có thể sử dụng tệp `apps.yaml` cục bộ hoặc tìm nạp phiên bản mới nhất từ kho lưu trữ trực tuyến.
- **Di động:** Không cần cài đặt cho chính trình cài đặt. Chỉ cần chạy tệp thực thi.
- **Nhận biết kiến trúc:** Tự động chọn phiên bản trình cài đặt chính xác (x86 hoặc x64) cho hệ thống của bạn.

## Cách thức hoạt động

1.  **Tải cấu hình:** Khi khởi chạy, trình cài đặt trước tiên sẽ tìm tệp `apps.yaml` trong thư mục của nó.
2.  **Dự phòng trực tuyến:** Nếu không tìm thấy cấu hình cục bộ, nó sẽ cố gắng tải xuống `apps.yaml` mới nhất từ URL trực tuyến được xác định trước.
3.  **Hiển thị ứng dụng:** Danh sách ứng dụng từ tệp YAML được phân tích cú pháp và hiển thị trong giao diện người dùng.
4.  **Cài đặt:** Người dùng chọn các ứng dụng mong muốn và nhấp vào "Cài đặt". Tiện ích sau đó sẽ tải xuống các tệp cần thiết và thực hiện các quy trình cài đặt và vá lỗi một cách âm thầm trong nền.

## Sử dụng

1.  Đảm bảo tệp `apps.yaml` được định cấu hình và đặt trong cùng thư mục với tệp thực thi (hoặc kho lưu trữ trực tuyến có thể truy cập được).
2.  Chạy `HieuckIT-App-Installer.exe`.
3.  Chọn các ứng dụng bạn muốn cài đặt từ danh sách.
4.  Nhấp vào nút **Cài đặt** và đợi quá trình hoàn tất.

## Cấu hình (`apps.yaml`)

Hành vi của ứng dụng được điều khiển bởi tệp `apps.yaml`, chứa danh sách các ứng dụng. Mỗi ứng dụng là một đối tượng có các thuộc tính sau:

```yaml
applications:
  - Name: "Example App"
    ProcessName: "example.exe"
    RegistryDisplayName: "Example App"
    InstallerArgs: "/S"
    DownloadLinks:
      - Name: "Installer"
        Url_x64: "https://example.com/installer_64.exe"
        Url_x86: "https://example.com/installer_32.exe"
    IsArchive: false
    PatchArgs: 'x -y "{patch_path}" -o"{install_dir}"'
    PatchLinks:
      - Name: "Patch"
        Url_x64: "https://example.com/patch_64.rar"
        Url_x86: "https://example.com/patch_32.rar"

```
- **Name:** Tên hiển thị của ứng dụng trong giao diện người dùng.
- **ProcessName:** Tên tiến trình thực thi để kiểm tra xem ứng dụng có đang chạy hay không hoặc để tạo lối tắt.
- **RegistryDisplayName:** Tên được tìm thấy trong Windows Registry để xác minh xem ứng dụng đã được cài đặt chưa.
- **InstallerArgs:** Các đối số dòng lệnh để cài đặt im lặng (ví dụ: `/S`, `/VERYSILENT`).
- **DownloadLinks:** Danh sách chứa các liên kết tải xuống cho trình cài đặt. `Url_x64` và `Url_x86` chỉ định các liên kết cho hệ thống 64-bit và 32-bit tương ứng.
- **IsArchive:** Đặt thành `true` nếu tệp đã tải xuống là kho lưu trữ zip/rar cần giải nén thay vì trình cài đặt tiêu chuẩn.
- **PatchArgs:** Các đối số dòng lệnh cho công cụ vá (7-Zip) để áp dụng các bản vá.
- **PatchLinks:** Danh sách các liên kết tải xuống cho các tệp vá.
