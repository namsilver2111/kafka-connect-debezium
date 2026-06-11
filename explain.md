# Giải thích Cấu hình Debezium SQL Server Source Connector

File cấu hình này dùng để khởi tạo một connector bắt các sự kiện thay đổi dữ liệu (CDC) từ SQL Server và đẩy lên Kafka.

## 1. Thông tin định danh của Connector

* **`name`**: Tên của connector instance.
    * *Giá trị trong bài*: `"sqlserver-teams-source-connector"`
    * *Options*: Bạn có thể đặt bất kỳ tên gì, miễn là unique (duy nhất) trong cụm Kafka Connect của bạn.

## 2. Các tham số hệ thống (Config)

* **`connector.class`**: Tên class Java sẽ thực thi nhiệm vụ của connector này.
    * *Giá trị trong bài*: `"io.debezium.connector.sqlserver.SqlServerConnector"`
    * *Options*: Bắt buộc phải là chuỗi này nếu bạn đang kết nối với SQL Server bằng Debezium.

* **`tasks.max`**: Số lượng luồng (thread/task) tối đa mà Kafka Connect có thể tạo ra để chạy connector này.
    * *Giá trị trong bài*: `"1"`
    * *Options*: Với Debezium SQL Server, bạn **luôn nên để là 1**. Việc đọc Transaction Log của SQL Server yêu cầu đọc theo đúng thứ tự tuần tự, nên không thể chia nhỏ cho nhiều tasks chạy song song được.

## 3. Thông tin kết nối Database

* **`database.hostname`**: Địa chỉ IP hoặc hostname của server chứa SQL Server.
    * *Giá trị trong bài*: `"host.docker.internal"` (truy cập localhost của máy host từ bên trong container Docker).
* **`database.port`**: Cổng kết nối của SQL Server.
    * *Giá trị mặc định*: `"1433"`
* **`database.user`** & **`database.password`**: Tài khoản SQL Server có đủ quyền (sysadmin hoặc db_owner + quyền CDC) để đọc dữ liệu.
* **`database.names`**: Tên cơ sở dữ liệu (Database) mà bạn muốn theo dõi.
    * *Giá trị trong bài*: `"sport"`
* **`database.encrypt`**: Có sử dụng mã hóa SSL/TLS khi kết nối với SQL Server hay không.
    * *Giá trị trong bài*: `"false"`
    * *Options*: `"true"` hoặc `"false"`.

## 4. Cấu hình Filter và Tên Topic

* **`topic.prefix`**: Tiền tố (prefix) cho tên của tất cả các Kafka topic mà connector này tự động tạo ra.
    * *Giá trị trong bài*: `"cdc"`
    * *Lưu ý*: Format tên topic sẽ là: `[topic.prefix].[schema].[table]`. Ví dụ bảng `teams` của bạn sẽ được đẩy vào topic tên là: `cdc.dbo.teams`.
* **`table.include.list`**: Danh sách các bảng cụ thể mà bạn muốn bắt dữ liệu CDC.
    * *Giá trị trong bài*: `"dbo.teams"`
    * *Options*: Cú pháp là `schema_name.table_name`. Nếu muốn bắt nhiều bảng, bạn phân tách bằng dấu phẩy (VD: `"dbo.teams,dbo.players"`). Có thể dùng regex. Nếu không khai báo, nó sẽ bắt tất cả các bảng đã bật CDC trong DB.

## 5. Lưu trữ lịch sử Schema (Schema History)
*Debezium cần lưu lại lịch sử cấu trúc (schema) của các bảng để biết cách map dữ liệu quá khứ một cách chính xác.*

* **`schema.history.internal.kafka.bootstrap.servers`**: Địa chỉ của Kafka cluster để Debezium lưu trữ schema history nội bộ (Dùng cho Debezium bản 2.x trở lên).
* **`schema.history.internal.kafka.topic`**: Tên topic dùng để lưu schema history.
    * *Lưu ý*: Topic này chỉ nội bộ Debezium dùng, bạn không nên manual xóa hay sửa dữ liệu trong này.
* **`database.history.kafka.bootstrap.servers`** & **`database.history.kafka.topic`**:
    * *Lưu ý quan trọng*: Đây là các properties cũ của Debezium 1.x. Ở Debezium 2.x, chúng đã bị **deprecated** (loại bỏ) và thay thế hoàn toàn bởi `schema.history.internal.*` ở trên. Bạn có thể giữ lại nếu dùng bản cũ, hoặc xóa đi nếu dùng bản mới.

## 6. Cấu hình Data Format (Converter)
*Định dạng dữ liệu của messages (key và value) khi đẩy vào Kafka.*

* **`key.converter`** & **`value.converter`**: Kiểu định dạng cho Key và Value của message.
    * *Giá trị trong bài*: `"org.apache.kafka.connect.json.JsonConverter"` (Định dạng JSON).
    * *Options phổ biến*:
        * `JsonConverter`: Dễ đọc, dễ debug, nhưng dung lượng lớn.
        * `AvroConverter` (io.confluent.connect.avro.AvroConverter): Tối ưu dung lượng, tốc độ cao, nhưng cần thiết lập thêm Schema Registry.
        * `StringConverter`: Dạng chuỗi thuần (hiếm dùng cho value của CDC).
* **`key.converter.schemas.enable`** & **`value.converter.schemas.enable`**: Có đính kèm luôn định nghĩa cấu trúc (schema) vào trong nội dung mỗi message JSON hay không.
    * *Giá trị trong bài*: `"true"`
    * *Options*: `"true"` hoặc `"false"`. Đặt là `"true"` thì message sẽ rất bự (vì chứa cả phần mô tả data), nhưng nhiều Kafka Sink Connectors (như JDBC Sink) lại yêu cầu phải có thuộc tính này mới chạy được. Nếu bạn tự viết consumer để đọc dữ liệu, hãy để `"false"` cho nhẹ.

## 7. Cấu hình Khác

* **`include.schema.changes`**: Có xuất các sự kiện thay đổi cấu trúc bảng (DDL - ví dụ: CREATE TABLE, ALTER TABLE) ra một topic riêng hay không.
    * *Giá trị trong bài*: `"true"`
    * *Options*: `"true"` hoặc `"false"`. Topic xuất ra sẽ có tên giống với giá trị của `topic.prefix`.
* **`snapshot.mode`**: Chế độ lấy dữ liệu lần đầu tiên (Snapshot) khi connector mới được khởi chạy.
    * *Giá trị trong bài*: `"initial"`
    * *Options*:
        * `"initial"` (Khuyên dùng): Quét toàn bộ dữ liệu hiện có trong bảng `dbo.teams` đẩy vào Kafka, sau đó mới bắt đầu stream các thay đổi mới (CDC).
        * `"initial_only"`: Quét toàn bộ dữ liệu hiện có xong rồi... dừng connector luôn (không stream CDC tiếp).
        * `"schema_only"`: Không lấy dữ liệu cũ hiện có. Chỉ lấy cấu trúc bảng và bắt đầu stream các thay đổi (Insert/Update/Delete) xảy ra **từ thời điểm connector chạy trở đi**.