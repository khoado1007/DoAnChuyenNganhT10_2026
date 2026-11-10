namespace ITManagement.Domain.Exceptions;

public class DomainException : Exception
{
    /// <summary>Khởi tạo ngoại lệ cơ sở của miền nghiệp vụ.</summary>
    public DomainException(string message) : base(message) { }
}

public class NotFoundException : DomainException
{
    /// <summary>Tạo ngoại lệ báo không tìm thấy thực thể theo mã định danh.</summary>
    public NotFoundException(string entityName, object id)
        : base($"{entityName} với id '{id}' không tồn tại.") { }
}

public class UnauthorizedException : DomainException
{
    /// <summary>Tạo ngoại lệ báo thao tác không được phép.</summary>
    public UnauthorizedException(string message = "Bạn không có quyền thực hiện thao tác này.")
        : base(message) { }
}

public class QuotaExceededException : DomainException
{
    /// <summary>Tạo ngoại lệ khi kích thước tệp vượt dung lượng còn lại.</summary>
    public QuotaExceededException(long fileSizeBytes, long availableBytes)
        : base($"Dung lượng tệp ({fileSizeBytes:N0} bytes) vượt quá hạn mức còn lại ({availableBytes:N0} bytes).") { }
}

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Tạo ngoại lệ chứa các lỗi kiểm tra dữ liệu.</summary>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Dữ liệu không hợp lệ. Kiểm tra các trường bị lỗi.")
    {
        Errors = errors.AsReadOnly();
    }
}

public class BusinessRuleException : DomainException
{
    /// <summary>Tạo ngoại lệ cho vi phạm quy tắc nghiệp vụ.</summary>
    public BusinessRuleException(string message) : base(message) { }
}
