Công cụ một lần dọn dữ liệu thử (tiền tố `QA`) và chuẩn hoá dữ liệu cũ trên CSDL đang chạy.

    dotnet run -- <đường dẫn tệp .env chứa ConnectionStrings__Default> survey   # chỉ đếm
    dotnet run -- <tệp .env> dry                                                # chạy thử, ROLLBACK
    dotnet run -- <tệp .env> apply                                              # COMMIT

Không nằm trong solution. Chuỗi kết nối chỉ được đọc từ tệp, không ghi ra màn hình.
