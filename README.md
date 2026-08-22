# SmsPing (Ver LTE) — tự nhận SMSC theo nhà mạng, PING không cần gõ AT

Công cụ PING SMS qua modem/SIM cắm trên PC. Dựng lại từ bản gốc, đã sửa để
**cắm SIM nhà mạng nào cũng tự PING được** mà không phải gõ lệnh AT tay.

## Cách hoạt động (đã sửa)

Khi bấm **Connect**, chương trình:

1. Dọn sạch bộ nhớ SMS (SM/ME/SR) để tránh `+CMS ERROR: Memory full`.
2. Đọc **IMSI** của SIM (`AT+CIMI`) — đọc được kể cả khi SMSC trên SIM trống —
   để nhận đúng nhà mạng (dự phòng `AT+CPSI?`, `AT+COPS?`).
3. **Nhét thẳng địa chỉ SMSC vào đầu chuỗi PDU** khi PING. Nhờ vậy modem luôn
   biết tổng đài để đẩy tin → **không còn `+CMS ERROR: SMSC address unknown`**,
   không phụ thuộc SMSC có lưu trong modem hay không. (`AT+CMGS=19` giữ nguyên
   vì độ dài này chỉ đếm phần TPDU, không tính octet SMSC.)

Hỗ trợ sẵn: **Mobifone, Vinaphone, Viettel, Vietnamobile, Gmobile**.

## Bảng SMSC (sửa trong `src/PduCodec` không cần — sửa map ở `src/frmMain.cs`)

| Nhà mạng | MNC | SMSC | Prefix nhét vào PDU |
|---|---|---|---|
| Mobifone (Nam) | 01 | +84900000023 | 07914809000020F3 |
| Vinaphone | 02 | +8491020005 | 06914819200050 |
| Viettel | 04 | +84980200030 | 07914889200030F0 |
| Vietnamobile | 05 | +84925252525 | 07914829252525F5 |
| Gmobile | 07 | +84995252525 | 07914899252525F5 |

Mobifone khác vùng: Bắc +84900000011, Trung +84900000017 (sửa dòng "01" trong
`SmscByNetwork` ở `src/frmMain.cs` nếu cần).

## Build ra SmsPing.exe (GitHub Actions — như đợt trước)

1. Đẩy toàn bộ thư mục này lên GitHub (nhánh `main`).
2. Tab **Actions** → workflow **Build SmsPing** chạy tự động (hoặc **Run workflow**).
3. Tải **Artifacts → SmsPing** (gồm `SmsPing.exe` + `SmsPing.exe.config`).
   Chạy trên Windows có .NET Framework 4.8.

Build tại chỗ (Windows có .NET SDK): `dotnet build src/SmsPing.csproj -c Release`
→ `src/bin/Release/net48/SmsPing.exe`.


## Kiểm tra tài khoản (USSD *101#)

Chế độ mạng đặt **tự động** (`AT+CNMP=2`) để USSD dùng được (LTE-only thường
báo `+CME ERROR: no network service`). Nút *Kiểm tra TK* sẽ chờ SIM đăng ký
mạng rồi mới gửi USSD; kết quả hiện ở khung RAW CODE.


## Đọc kết quả online/offline

Khi có báo cáo giao hàng (`+CDS`) trả về, chương trình **tự giải mã và hiện
kết quả** ở khung dưới, ví dụ:

    PING SMS có CMGS: 200
    Đến SĐT 0967829131
    Được SMSC nhận lúc: 17:42:33, ngay 22/08/2026, phát lúc: 17:42:34, ngay 22/08/2026
    Có kết quả: SDT PING ONLINE.

`SDT PING ONLINE.` = số đó đang bật máy (đã nhận tin). Các mã khác (SDT ban,
SDT Khong hoi dap, Het han...) tương ứng máy bận/tắt/không tồn tại. Vẫn có nút
**DECODE vùng chọn** để giải mã thủ công 1 đoạn báo cáo bất kỳ.

## Ghi chú

- Danh sách IMEI thiết bị hợp lệ ở `AllowedImei` trong `src/frmMain.cs`.
  Thêm IMEI modem mới vào đó nếu muốn dùng máy khác (hoặc bỏ khối kiểm tra IMEI).
