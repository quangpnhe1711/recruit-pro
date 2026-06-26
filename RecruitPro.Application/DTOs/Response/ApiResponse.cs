namespace RecruitPro.Application.DTOs.Response
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Stable machine-readable error code (ERROR-CONTRACT.md). Null on success and on responses
        /// that have not yet been migrated to the error-code contract.
        /// </summary>
        public string? ErrorCode { get; set; }
        public T? Data { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
        public object? Extra { get; set; }

        private ApiResponse(
            bool success,
            int statusCode,
            string message,
            T? data = default,
            Dictionary<string, string[]>? errors = null,
            object? extra = null,
            string? errorCode = null)
        {
            Success = success;
            StatusCode = statusCode;
            Message = message;
            Data = data;
            Errors = errors;
            Extra = extra;
            ErrorCode = errorCode;
        }

        public static ApiResponse<T> Ok(T data, string message = "Thành công")
        {
            return new ApiResponse<T>(true, 200, message, data);
        }

        public static ApiResponse<T> Created(T data, string message = "Tạo thành công")
        {
            return new ApiResponse<T>(true, 201, message, data);
        }

        public static ApiResponse<T> NoContent(string message = "Không có dữ liệu")
        {
            return new ApiResponse<T>(true, 204, message);
        }

        public static ApiResponse<T> BadRequest(string message, string? errorCode = null)
        {
            return new ApiResponse<T>(false, 400, message, errorCode: errorCode);
        }

        public static ApiResponse<T> ValidationError(string message, Dictionary<string, string[]> errors)
        {
            return new ApiResponse<T>(false, 400, message, default, errors);
        }

        public static ApiResponse<T> Unauthorized(string message = "Không có quyền truy cập", string? errorCode = null)
        {
            return new ApiResponse<T>(false, 401, message, errorCode: errorCode);
        }

        public static ApiResponse<T> NotFound(string message = "Không tìm thấy dữ liệu", string? errorCode = null)
        {
            return new ApiResponse<T>(false, 404, message, errorCode: errorCode);
        }

        public static ApiResponse<T> Forbidden(string message = "Bạn không có quyền", string? errorCode = null)
        {
            return new ApiResponse<T>(false, 403, message, errorCode: errorCode);
        }

        public static ApiResponse<T> Conflict(string message, object? extra = null, string? errorCode = null)
        {
            return new ApiResponse<T>(false, 409, message, default, null, extra, errorCode);
        }

        public static ApiResponse<T> UnprocessableEntity(string message, object? extra = null, string? errorCode = null)
        {
            return new ApiResponse<T>(false, 422, message, default, null, extra, errorCode);
        }

        public static ApiResponse<T> Error(string message = "Lỗi hệ thống", object? extra = null)
        {
            return new ApiResponse<T>(false, 500, message, default, null, extra);
        }
    }
}
