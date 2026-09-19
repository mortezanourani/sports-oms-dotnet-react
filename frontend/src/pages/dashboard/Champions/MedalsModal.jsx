import { useState } from 'react';
import { Modal, Table, Button, Popconfirm, message, Form, Select, Input, InputNumber, Space, Divider } from 'antd';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { useChampion, useAddMedal, useDeleteMedal } from '../../../hooks/useChampions';
import { MEDAL_TYPE_LABELS, AGE_GROUP_LABELS, TOURNAMENT_LEVEL_LABELS } from '../../../constants/labels';

export default function MedalsModal({ championId, open, onClose }) {
    const { data: champion, isLoading } = useChampion(championId);
    const [showAddForm, setShowAddForm] = useState(false);
    const [form] = Form.useForm();

    const addMutation = useAddMedal();
    const deleteMutation = useDeleteMedal();

    const handleAdd = async (values) => {
        try {
            await addMutation.mutateAsync({ championId, payload: { ...values, count: values.count || 1 } });
            message.success('مدال ثبت شد');
            form.resetFields();
            setShowAddForm(false);
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در ثبت مدال');
        }
    };

    const handleDelete = async (medalId) => {
        try {
            await deleteMutation.mutateAsync({ championId, medalId });
            message.success('حذف شد');
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    const columns = [
        { title: 'نوع', dataIndex: 'medalType', render: (v) => MEDAL_TYPE_LABELS[v] || v },
        { title: 'گروه سنی', dataIndex: 'ageGroup', render: (v) => AGE_GROUP_LABELS[v] || v },
        { title: 'سطح', dataIndex: 'level', render: (v) => TOURNAMENT_LEVEL_LABELS[v] || v },
        { title: 'رقابت', dataIndex: 'competitionName' },
        { title: 'سال', dataIndex: 'year' },
        { title: 'تعداد', dataIndex: 'count' },
        { title: 'رشته', dataIndex: 'field' },
        {
            title: '', key: 'actions',
            render: (_, record) => (
                <Popconfirm title="حذف این مدال؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                    <Button icon={<DeleteOutlined />} size="small" danger />
                </Popconfirm>
            ),
        },
    ];

    return (
        <Modal
            title={champion ? `مدال‌های ${champion.fullName}` : 'مدال‌ها'}
            open={open}
            onCancel={onClose}
            footer={null}
            width={800}
            destroyOnClose
        >
            <Table
                rowKey="id"
                size="small"
                columns={columns}
                dataSource={champion?.medals}
                loading={isLoading}
                pagination={false}
                locale={{ emptyText: 'هنوز مدالی ثبت نشده است' }}
            />

            <Divider />

            {!showAddForm ? (
                <Button icon={<PlusOutlined />} onClick={() => setShowAddForm(true)}>افزودن مدال جدید</Button>
            ) : (
                <Form form={form} layout="vertical" onFinish={handleAdd}>
                    <Space wrap>
                        <Form.Item name="medalType" label="نوع مدال" rules={[{ required: true }]} style={{ width: 140 }}>
                            <Select options={Object.entries(MEDAL_TYPE_LABELS).map(([value, label]) => ({ value, label }))} />
                        </Form.Item>
                        <Form.Item name="ageGroup" label="گروه سنی" rules={[{ required: true }]} style={{ width: 160 }}>
                            <Select options={Object.entries(AGE_GROUP_LABELS).map(([value, label]) => ({ value, label }))} />
                        </Form.Item>
                        <Form.Item name="level" label="سطح مسابقه" rules={[{ required: true }]} style={{ width: 150 }}>
                            <Select options={Object.entries(TOURNAMENT_LEVEL_LABELS).map(([value, label]) => ({ value, label }))} />
                        </Form.Item>
                        <Form.Item name="year" label="سال" rules={[{ required: true }]} style={{ width: 100 }}>
                            <InputNumber min={1300} max={1500} style={{ width: '100%' }} />
                        </Form.Item>
                        <Form.Item name="count" label="تعداد" initialValue={1} style={{ width: 90 }}>
                            <InputNumber min={1} style={{ width: '100%' }} />
                        </Form.Item>
                    </Space>
                    <Form.Item name="competitionName" label="نام مسابقه" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="field" label="رشته / ماده مسابقه">
                        <Input placeholder="مثلاً: وزن ۷۴ کیلوگرم" />
                    </Form.Item>
                    <Space>
                        <Button type="primary" htmlType="submit" loading={addMutation.isPending}>ثبت مدال</Button>
                        <Button onClick={() => { setShowAddForm(false); form.resetFields(); }}>انصراف</Button>
                    </Space>
                </Form>
            )}
        </Modal>
    );
}
