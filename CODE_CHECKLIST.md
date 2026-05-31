# Code Checklist

- [ ] Controller chỉ làm nhiệm vụ nhận request, gọi service, và trả HTTP response.
- [ ] Service chứa business logic; controller không xử lý nghiệp vụ.
- [ ] Service trả về `ApiResponse<T>` khi cần thống nhất `success/statusCode/message/data`.
- [ ] Controller trả về `IActionResult` hoặc `ActionResult<T>` để quyết định HTTP status code.
- [ ] Dùng `async`/`await` cho mọi luồng I/O và đặt hậu tố `Async` cho method.
- [ ] Đăng ký đầy đủ service/repository vào DI trước khi dùng.
- [ ] DTO, entity, interface phải đặt tên nhất quán; tránh lệch số ít/số nhiều.
- [ ] Read-only query nên dùng `AsNoTracking()` khi có EF Core.
- [ ] Mapping entity -> DTO thực hiện ở application layer, không để controller tự map.
- [ ] Không nuốt lỗi bằng `catch` rỗng; chỉ catch khi có xử lý rõ ràng.
- [ ] Không sửa lan sang phần không liên quan.
- [ ] Sau khi đổi code, build lại project để kiểm tra lỗi biên dịch.
- [ ] Sử dụng mapper để map dto trả về.