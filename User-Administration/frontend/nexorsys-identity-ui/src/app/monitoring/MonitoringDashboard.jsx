import React, { useState, useEffect } from "react";
import { Card, Table, Tag, Row, Col, Statistic, Alert } from "antd";
import {
  SafetyCertificateOutlined,
  WarningOutlined,
  CloseCircleOutlined,
  CheckCircleOutlined,
  DesktopOutlined,
} from "@ant-design/icons";
import monitoringService from "../../services/monitoringService";
import { requestFailureMessage } from "../../security/authState";

export default function MonitoringDashboard() {
  const [stats, setStats] = useState({
    totalWorkstations: 0,
    offlineWorkstations: 0,
    successfulLoginsToday: 0,
    failedLoginsToday: 0,
    expiringEnrollments: 0,
  });
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      setLoadError(null);
      const statsData = await monitoringService.getStats();
      const eventsData = await monitoringService.getRecentEvents();

      setStats(statsData);
      setEvents(eventsData);
    } catch (error) {
      setLoadError(requestFailureMessage(error, "monitoring"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ padding: 24 }}>
      <h2>IAM Monitoring & Telemetry</h2>
      {loadError && <Alert type="error" showIcon message={loadError} style={{ marginBottom: 16 }} />}

      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col span={6}>
          <Card>
            <Statistic
              title="Successful Logins (Today)"
              value={stats.successfulLoginsToday}
              valueStyle={{ color: "#3f8600" }}
              prefix={<CheckCircleOutlined />}
            />
          </Card>
        </Col>
        <Col span={6}>
          <Card>
            <Statistic
              title="Failed Logins (Today)"
              value={stats.failedLoginsToday}
              valueStyle={{ color: "#cf1322" }}
              prefix={<CloseCircleOutlined />}
            />
          </Card>
        </Col>
        <Col span={6}>
          <Card>
            <Statistic
              title="Offline Workstations"
              value={stats.offlineWorkstations}
              suffix={`/ ${stats.totalWorkstations}`}
              valueStyle={{
                color: stats.offlineWorkstations > 0 ? "#faad14" : "#3f8600",
              }}
              prefix={<DesktopOutlined />}
            />
          </Card>
        </Col>
        <Col span={6}>
          <Card>
            <Statistic
              title="Expiring Enrollments (7 Days)"
              value={stats.expiringEnrollments}
              valueStyle={{
                color: stats.expiringEnrollments > 0 ? "#cf1322" : "#3f8600",
              }}
              prefix={<SafetyCertificateOutlined />}
            />
          </Card>
        </Col>
      </Row>

      <Card title="Recent Authentication Events">
        <Table
          dataSource={events}
          rowKey="id"
          loading={loading}
          columns={[
            {
              title: "Timestamp",
              dataIndex: "timestamp",
              key: "timestamp",
              render: (val) => new Date(val).toLocaleString(),
            },
            { title: "User", dataIndex: "user", key: "user" },
            {
              title: "Provider",
              dataIndex: "providerType",
              key: "providerType",
            },
            { title: "Event", dataIndex: "eventType", key: "eventType" },
            {
              title: "Result",
              dataIndex: "result",
              key: "result",
              render: (status) => (
                <Tag color={status === "Success" ? "green" : "red"}>
                  {status}
                </Tag>
              ),
            },
            { title: "Reason", dataIndex: "reason", key: "reason" },
            { title: "IP Address", dataIndex: "ipAddress", key: "ipAddress" },
          ]}
        />
      </Card>
    </div>
  );
}
