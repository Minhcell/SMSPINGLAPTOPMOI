# SmsPing (Ver LTE) — bản dựng lại có tự gắn SMSC tất cả nhà mạng VN

Công cụ PING SMS qua modem/SIM cắm trên PC, dựng lại từ bản gốc, đã sửa:

- **Gắn sẵn SMSC cho tất cả nhà mạng VN**: Mobifone, Vinaphone, Viettel,
  **Vietnamobile** (mới), **Gmobile** (mới). Khi Connect, chương trình đọc
  IMSI của SIM (`AT+CIMI`) để nhận đúng nhà mạng rồi tự set `AT+CSCA`, có
  dự phòng theo mạng đang bắt (`AT+CPSI?`) và đọc lại để xác nhận.
- **Chống lỗi `+CMS ERROR: Memory full`**: mỗi lần Connect tự dọn sạch mọi
  vùng nhớ SMS (SM/ME/SR) rồi ưu tiên lưu tin vào bộ nhớ modem.

## Cách build ra SmsPing.exe (GitHub Actions)

1. Đẩy toàn bộ thư mục này lên GitHub (nhánh `main`).
2. Vào tab **Actions** → workflow **Build SmsPing** chạy tự động (hoặc bấm
   **Run workflow**).
3. Tải file trong mục **Artifacts → SmsPing** (gồm `SmsPing.exe` +
   `SmsPing.exe.config`). Chạy trên Windows có .NET Framework 4.8.

Build tại chỗ (máy Windows có .NET SDK):

    dotnet build src/SmsPing.csproj -c Release

Kết quả: `src/bin/Release/net48/SmsPing.exe`.

## Bảng SMSC (sửa trong src/frmMain.cs nếu cần)

| Nhà mạng | MNC | SMSC |
|---|---|---|
| Mobifone (Nam) | 01 | +84900000023 (Bắc +84900000011, Trung +84900000017) |
| Vinaphone | 02 | +8491020005 |
| Viettel | 04 | +84980200030 |
| Vietnamobile | 05 | +84925252525 |
| Gmobile | 07 | +84995252525 |

## Ghi chú

- Danh sách IMEI thiết bị hợp lệ nằm ở `AllowedImei` trong `src/frmMain.cs`.
  Thêm IMEI modem mới vào đó nếu muốn dùng máy khác.
