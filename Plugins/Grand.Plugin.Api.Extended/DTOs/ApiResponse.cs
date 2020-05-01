using System.Collections.Generic;

namespace Grand.Plugin.Api.Extended.DTOs
{
    public class ApiResponse<TData>
    {
        public bool Success { get; set; }

        public bool Redirect { get; set; }

        public TData Data { get; set; }

        public string Message { get; set; }

        public bool HasError => !Success;

        public bool HasData => Data != null;

        public List<string> AggregateErrors { get; private set; } = new List<string>();

        public static ApiResponse<TData> SuccessResult(TData data)
        {
            return new ApiResponse<TData>
            {
                Success = true,
                Data = data
            };
        }

        public static ApiResponse<TData> RedirectResult(TData data)
        {
            return new ApiResponse<TData> {
                Redirect = true,
                Data = data
            };
        }

        public static ApiResponse<TData> FailResult(string error)
        {
            return new ApiResponse<TData> {
                Success = false,
                Data = default(TData),
                Message = error
            };
        }

        public static ApiResponse<TData> FailResult(string error, TData data)
        {
            return new ApiResponse<TData> {
                Success = false,
                Data = data,
                Message = error
            };
        }
    }
}
