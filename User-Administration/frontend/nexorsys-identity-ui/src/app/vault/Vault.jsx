import React, { useState, useEffect } from "react";
import { Table, Card, Button, Modal, Form, Input, Select, Popconfirm, Alert, Space, Tag, notification } from "antd";
import { PlusOutlined, LockOutlined, ReloadOutlined, DeleteOutlined, StopOutlined } from "@ant-design/icons";
import dayjs from "dayjs";
import axios from "axios";

const { Option } = Select;

export default function Vault() {
  const [isAddModalVisible, setIsAddModalVisible] = useState(false);
  const [isRotateModalVisible, setIsRotateModalVisible] = useState(false);
  const [selectedRecord, setSelectedRecord] = useState(null);
  const [form] = Form.useForm();
  const [vaultRecords, setVaultRecords] = useState([]);
  const [loading, setLoading] = useState(false);

  const fetchVaultRecords = async () => {
    setLoading(true);
    try {
      const response = await axios.get("http://localhost:3005/api/vault");
      setVaultRecords(response.data);
    } catch (error) {
      notification.error({ message: "Erreur", description: "Impossible de charger le coffre-fort." });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchVaultRecords();
  }, []);

  const openAddModal = () => {
    form.resetFields();
    setIsAddModalVisible(true);
  };

  const handleAdd = async () => {
    try {
      const values = await form.validateFields();
      await axios.post("http://localhost:3005/api/vault", values);
      notification.success({ message: "Succès", description: "Identifiant ajouté avec succès." });
      setIsAddModalVisible(false);
      fetchVaultRecords();
    } catch (error) {
      notification.error({ message: "Erreur", description: "L'ajout a échoué." });
    }
  };

  const openRotateModal = (record) => {
    setSelectedRecord(record);
    form.resetFields();
    setIsRotateModalVisible(true);
  };

  const handleRotate = async () => {
    try {
      const values = await form.validateFields();
      await axios.post(`http://localhost:3005/api/vault/${selectedRecord.id}/rotate`, { newSecret: values.newSecret });
      notification.success({ message: "Succès", description: "Rotation effectuée avec succès." });
      setIsRotateModalVisible(false);
      fetchVaultRecords();
    } catch (error) {
      notification.error({ message: "Erreur", description: "La rotation a échoué." });
    }
  };

  const handleRevoke = async (id) => {
    try {
      await axios.post(`http://localhost:3005/api/vault/${id}/revoke`);
      notification.success({ message: "Succès", description: "Identifiant révoqué avec succès." });
      fetchVaultRecords();
    } catch (error) {
      notification.error({ message: "Erreur", description: "La révocation a échoué." });
    }
  };

  const handleDelete = async (id) => {
    try {
      await axios.delete(`http://localhost:3005/api/vault/${id}`);
      notification.success({ message: "Succès", description: "Identifiant supprimé." });
      fetchVaultRecords();
    } catch (error) {
      notification.error({ message: "Erreur", description: "La suppression a échoué." });
    }
  };

  const columns = [
    { title: "Identifiant/Nom", dataIndex: "name", key: "name" },
    { title: "Type", dataIndex: "credentialType", key: "credentialType" },
    { title: "Compte utilisateur", dataIndex: "username", key: "username" },
    { title: "Statut", dataIndex: "status", key: "status", render: (status) => <Tag color={status === "ACTIVE" ? "green" : "red"}>{status}</Tag> },
    { title: "Dernière rotation", dataIndex: "lastRotatedAt", key: "lastRotatedAt", render: (val) => val ? dayjs(val).format("YYYY-MM-DD HH:mm") : "Jamais" },
    {
      title: "Actions",
      key: "actions",
      render: (_, record) => (
        <Space>
          <Button
            icon={<ReloadOutlined />}
            onClick={() => openRotateModal(record)}
            disabled={record.status === "REVOKED"}
            title="Faire une rotation"
          />
          <Popconfirm title="Révoquer cet identifiant ?" onConfirm={() => handleRevoke(record.id)} disabled={record.status === "REVOKED"}>
            <Button icon={<StopOutlined />} disabled={record.status === "REVOKED"} />
          </Popconfirm>
          <Popconfirm title="Supprimer définitivement ?" onConfirm={() => handleDelete(record.id)}>
            <Button danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ padding: 24 }}>
      <Space direction="vertical" style={{ width: "100%", marginBottom: 16 }}>
        <Alert
          message="Coffre-fort protégé (Vault Protected)"
          description="Les secrets sont chiffrés avec AES-256-GCM. Aucun secret n'est exposé en clair via cette interface."
          type="success"
          showIcon
          icon={<LockOutlined />}
        />
      </Space>

      <Card
        title="Gestion du Coffre-fort"
        extra={
          <Button type="primary" icon={<PlusOutlined />} onClick={openAddModal}>
            Ajouter un identifiant
          </Button>
        }
      >
        <Table
          dataSource={vaultRecords}
          columns={columns}
          rowKey="id"
          loading={loading}
          locale={{ emptyText: "Aucun identifiant trouvé." }}
        />
      </Card>

      <Modal
        title="Ajouter au Coffre-fort"
        open={isAddModalVisible}
        onCancel={() => setIsAddModalVisible(false)}
        onOk={handleAdd}
        okText="Enregistrer"
        cancelText="Annuler"
      >
        <Form form={form} layout="vertical">
          <Form.Item name="applicationId" label="Application ID" rules={[{ required: true }]}>
            <Input placeholder="00000000-0000-0000-0000-000000000000" />
          </Form.Item>
          <Form.Item name="name" label="Nom de l'identifiant" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="username" label="Compte utilisateur">
            <Input />
          </Form.Item>
          <Form.Item name="secret" label="Secret (Mot de passe/Token)" rules={[{ required: true }]}>
            <Input.Password />
          </Form.Item>
          <Form.Item name="credentialType" label="Type de secret">
            <Select defaultValue="PASSWORD">
              <Option value="PASSWORD">Mot de passe</Option>
              <Option value="TOKEN">Token API</Option>
            </Select>
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="Rotation de l'identifiant"
        open={isRotateModalVisible}
        onCancel={() => setIsRotateModalVisible(false)}
        onOk={handleRotate}
        okText="Faire une rotation"
        cancelText="Annuler"
      >
        <Form form={form} layout="vertical">
          <Form.Item name="newSecret" label="Nouveau Secret" rules={[{ required: true }]}>
            <Input.Password />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
