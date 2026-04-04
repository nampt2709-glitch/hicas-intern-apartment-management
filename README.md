**Áp dụng các migration** (cập nhật cơ sở dữ liệu):

```powershell

dotnet ef database update `

--project src/ApartmentManagement.API/ApartmentManagement.API.csproj `

--startup-project src/ApartmentManagement.API/ApartmentManagement.API.csproj
```

**Xóa cơ sở dữ liệu** (mất toàn bộ dữ liệu — sử dụng khi thay thế các migration hoặc đặt lại cơ sở dữ liệu phát triển, sau đó chạy lại lệnh `database update`):

```powershell

dotnet ef database drop --force `

--project src/ApartmentManagement.API/ApartmentManagement.API.csproj `

--startup-project src/ApartmentManagement.API/ApartmentManagement.API.csproj
```

Sau khi **xóa**, hãy chạy lại lệnh **`dotnet ef database update`**

======================================                         ==========================================================
Dùng và chạy project Seed Data để generate ra dữ liệu trên Database
Xong dùng import postman-collection.json và swagger-collection.json lên Postman và Swagger để test.
Khi nào muốn clean up thì chạy lại project Seed Data rồi chọn cleanup (xóa) để xóa hết seed data.