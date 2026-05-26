namespace RecruitPro.Application.DTOs.Response
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public T? Data { get; set; }

        private ApiResponse(
            bool success,
            int statusCode,
            string message,
            T? data = default)
        {
            Success = success;
            StatusCode = statusCode;
            Message = message;
            Data = data;
        }

        // 200
        public static ApiResponse<T> Ok(
            T data,
            string message = "Success")
        {
            return new ApiResponse<T>(
                true,
                200,
                message,
                data
            );
        }

        // 201
        public static ApiResponse<T> Created(
            T data,
            string message = "Created successfully")
        {
            return new ApiResponse<T>(
                true,
                201,
                message,
                data
            );
        }

        // 204
        public static ApiResponse<T> NoContent(
            string message = "No content")
        {
            return new ApiResponse<T>(
                true,
                204,
                message
            );
        }

        // 400
        public static ApiResponse<T> BadRequest(
            string message)
        {
            return new ApiResponse<T>(
                false,
                400,
                message
            );
        }

        // 401
        public static ApiResponse<T> Unauthorized(
            string message = "Unauthorized")
        {
            return new ApiResponse<T>(
                false,
                401,
                message
            );
        }

        // 404
        public static ApiResponse<T> NotFound(
            string message = "Resource not found")
        {
            return new ApiResponse<T>(
                false,
                404,
                message
            );
        }

        // 500
        public static ApiResponse<T> Error(
            string message = "Internal server error")
        {
            return new ApiResponse<T>(
                false,
                500,
                message
            );
        }
    }
}