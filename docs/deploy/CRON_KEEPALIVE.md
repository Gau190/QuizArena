# Cron Keep-Alive for Render + Somee

Mục tiêu:

- Gọi Render định kỳ để giảm cold start.
- Gọi endpoint có query DB để Somee SQL Server có activity.

Endpoint đã có:

```text
/healthz
/health/db
```

Nên dùng:

```text
https://<render-service>.onrender.com/health/db
```

Endpoint này chạy nhẹ:

- Kiểm tra kết nối DB.
- Đếm `Users`.
- Không cần đăng nhập.
- Không trả secret.

## Cách 1: cron-job.org

1. Vào https://cron-job.org
2. Tạo account miễn phí.
3. Create cronjob.
4. URL:

```text
https://<render-service>.onrender.com/health/db
```

5. Schedule:

```text
Every 10 minutes
```

6. Method:

```text
GET
```

7. Timeout:

```text
30 seconds
```

8. Save.

## Cách 2: UptimeRobot

1. Vào https://uptimerobot.com
2. Add New Monitor.
3. Monitor Type:

```text
HTTP(s)
```

4. URL:

```text
https://<render-service>.onrender.com/health/db
```

5. Interval:

```text
5 minutes
```

## Lưu ý quan trọng

Render free vẫn có thể sleep theo chính sách của họ. Ping định kỳ giúp giảm cold start, nhưng không phải cam kết production uptime.

Somee free có thể có chính sách xóa nếu không hoạt động lâu. Ping `/health/db` tạo activity đến DB, phù hợp hơn ping trang tĩnh.

Không nên ping quá dày. Dùng 5-10 phút/lần là đủ.

## Test nhanh

Mở browser:

```text
https://<render-service>.onrender.com/health/db
```

Kết quả đúng:

```json
{
  "status": "ok",
  "db": "ok",
  "users": 4,
  "utc": "..."
}
```
