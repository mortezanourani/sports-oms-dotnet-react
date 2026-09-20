import { useState } from 'react';
import { Table, Button, Space, Typography, Popconfirm, message, Modal, Form, Input } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, HistoryOutlined } from '@ant-design/icons';
import {
  useFederations, useCreateFederation, useUpdateFederation, useDeleteFederation,
} from '../../../hooks/useFederations';
import PositionsModal from './PositionsModal';

const { Title } = Typography;

export default function FederationsList() {
  const { data: federations, isLoading } = useFederations();
  const [modalOpen, setModalOpen] = useState(false);
  const [editingFederation, setEditingFederation] = useState(null);
  const [positionsFederation, setPositionsFederation] = useState(null);
  const [form] = Form.useForm();

  const createMutation = useCreateFederation();
  const updateMutation = useUpdateFederation();
  const deleteMutation = useDeleteFederation();

  const openCreate = () => {
    setEditingFederation(null);
    form.resetFields();
    setModalOpen(true);
  };

  const openEdit = (federation) => {
    setEditingFederation(federation);
    form.setFieldsValue(federation);
    setModalOpen(true);
  };

  const handleFinish = async (values) => {
    try {
      if (editingFederation) {
        await updateMutation.mutateAsync({ id: editingFederation.id, payload: values });
      } else {
        await createMutation.mutateAsync(values);
      }
      message.success('با موفقیت ذخیره شد');
      setModalOpen(false);
    } catch (err) {
      message.error(err.response?.data?.detail || 'خطایی رخ داد');
    }
  };

  const handleDelete = async (id) => {
    try {
      await deleteMutation.mutateAsync(id);
      message.success('حذف شد');
    } catch (err) {
      message.error(err.response?.data?.detail || 'خطا در حذف');
    }
  };

  const columns = [
    { title: 'نام فدراسیون', dataIndex: 'name' },
    { title: 'نوع ورزش', dataIndex: 'sportType' },
    {
      title: 'عملیات', key: 'actions',
      render: (_, record) => (
        <Space>
          <Button icon={<HistoryOutlined />} size="small" onClick={() => setPositionsFederation(record)}>
            سوابق مسئولین
          </Button>
          <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)} />
          <Popconfirm title="حذف این فدراسیون؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
            <Button icon={<DeleteOutlined />} size="small" danger />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Title level={4}>فدراسیون‌ها</Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>افزودن فدراسیون جدید</Button>
      </div>

      <Table rowKey="id" columns={columns} dataSource={federations} loading={isLoading} pagination={false} />

      <Modal
        title={editingFederation ? 'ویرایش فدراسیون' : 'افزودن فدراسیون جدید'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        okText="ذخیره"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={handleFinish}>
          <Form.Item name="name" label="نام فدراسیون" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="sportType" label="نوع ورزش" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
        </Form>
      </Modal>

      <PositionsModal
        federation={positionsFederation}
        open={!!positionsFederation}
        onClose={() => setPositionsFederation(null)}
      />
    </div>
  );
}
