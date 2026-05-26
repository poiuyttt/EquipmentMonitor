using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace EquipmentMonitorDay1
{
    /// <summary>
    /// SQLite 数据库帮助类
    /// 存储设备历史数据，支持按时间查询
    /// </summary>
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper()
        {
            // 数据库文件存在 exe 同级的 Data 目录
            string dbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dbDir);

            string dbPath = Path.Combine(dbDir, "EquipmentData.db");
            _connectionString = $"Data Source={dbPath};Version=3;";

            // 首次使用自动建表
            InitializeDatabase();
        }

        /// <summary>
        /// 建表（如果不存在）
        /// </summary>
        private void InitializeDatabase()
        {
            string sql =
                @"
                CREATE TABLE IF NOT EXISTS DeviceHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceName TEXT NOT NULL,
                    Value REAL NOT NULL DEFAULT 0,
                    Unit TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    RecordTime TEXT NOT NULL
                 );
                 CREATE TABLE IF NOT EXISTS AlarmRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceName TEXT NOT NULL,
                    AlarmValue REAL NOT NULL,
                    AlarmDesc TEXT NOT NULL,
                    RecordTime TEXT NOT NULL
                 )";

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 旧表迁移：尝试加 Value 列（如果不存在）
                try
                {
                    using (
                        var cmd = new SQLiteCommand(
                            "ALTER TABLE DeviceHistory ADD COLUMN Value REAL NOT NULL DEFAULT 0",
                            conn
                        )
                    )
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // 列已存在则忽略
                }
            }
        }

        /// <summary>
        /// 插入一条设备记录
        /// </summary>
        public void InsertRecord(string deviceName, double value, string unit, string status)
        {
            string sql =
                @"
                INSERT INTO DeviceHistory (DeviceName, Value, Unit, Status, RecordTime)
                VALUES (@deviceName, @value, @unit, @status, @time)";
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@deviceName", deviceName);
                    cmd.Parameters.AddWithValue("@value", value);
                    cmd.Parameters.AddWithValue("@unit", unit);
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue(
                        "@time",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 查询历史数据（按时间范围）
        /// </summary>
        public List<DeviceHistoryRecord> QueryHistory(
            string deviceName,
            DateTime startTime,
            DateTime endTime
        )
        {
            var results = new List<DeviceHistoryRecord>();

            string sql =
                @"
                SELECT DeviceName, Value, Unit, Status, RecordTime
                FROM DeviceHistory
                WHERE (@name = '' OR DeviceName = @name) AND RecordTime >= @start AND RecordTime <= @end                
                ORDER BY RecordTime ASC";

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", deviceName);
                    cmd.Parameters.AddWithValue(
                        "@start",
                        startTime.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                    cmd.Parameters.AddWithValue("@end", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(
                                new DeviceHistoryRecord
                                {
                                    DeviceName = reader["DeviceName"].ToString(),
                                    Value = Convert.ToDouble(reader["Value"]),
                                    Unit = reader["Unit"].ToString(),
                                    Status = reader["Status"].ToString(),
                                    RecordTime = DateTime.Parse(reader["RecordTime"].ToString()),
                                }
                            );
                        }
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// 清理 30 天前的旧数据
        /// </summary>
        public void CleanupOldData()
        {
            string sql =
                @"
                DELETE FROM DeviceHistory
                WHERE RecordTime < @date";

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue(
                        "@date",
                        DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss")
                    );
                    cmd.ExecuteNonQuery();
                }
            }
        }




        public void InsertAlarmRecord(string deviceName, double value, string desc)
        {
            string sql =
                @"
                INSERT INTO AlarmRecords (DeviceName, AlarmValue, AlarmDesc, RecordTime)
                VALUES (@deviceName, @alarmValue, @alarmDesc, @time)";
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@deviceName", deviceName);
                    cmd.Parameters.AddWithValue("@alarmValue", value);
                    cmd.Parameters.AddWithValue("@alarmDesc", desc);
                    cmd.Parameters.AddWithValue(
                        "@time",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                    cmd.ExecuteNonQuery();
                }
            }
        }






        /// <summary>
        /// 历史记录模型
        /// </summary>
        public class DeviceHistoryRecord
        {
            public string DeviceName { get; set; }
            public double Value { get; set; }
            public string Unit { get; set; }
            public string Status { get; set; }
            public DateTime RecordTime { get; set; }
        }
    }
}
