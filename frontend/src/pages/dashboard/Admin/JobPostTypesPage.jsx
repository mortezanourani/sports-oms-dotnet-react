import { useState } from 'react';
import {
    Button, Form, Input, List, Modal, Space, Table, Typography, Upload, message,
} from 'antd';
import {
    DownloadOutlined, EditOutlined, PlusOutlined, UploadOutlined,
} from '@ant-design/icons';
import {
    downloadJobPostTypesImportTemplate,
} from '../../../api/jobPostTypes';
import {
    useCreateJobPostType, useImportJobPostTypes, useJobPostTypes, useUpdateJobPostType,
} from '../../../hooks/useJobPostTypes';

const { Title } = Typography;

export default function JobPostTypesPage() {
    const { data: jobPostTypes = [], isLoading } = useJobPostTypes();
    const [form] = Form.useForm();
    const [formOpen, setFormOpen] = useState(false);
    const [importOpen, setImportOpen] = useState(false);
    const [editingType, setEditingType] = useState(null);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);

    const createMutation = useCreateJobPostType();
    const updateMutation = useUpdateJobPostType();
    const importMutation = useImportJobPostTypes();
    const formPending = createMutation.isPending || updateMutation.isPending;

    const openCreate = () => {
        setEditingType(null);
        form.resetFields();
        setFormOpen(true);
    };

    const openEdit = (jobPostType) => {
        setEditingType(jobPostType);
        form.setFieldsValue({ id: jobPostType.id, title: jobPostType.title });
        setFormOpen(true);
    };

    const handleFinish = async (values) => {
        try {
            if (editingType) {
                await updateMutation.mutateAsync({
                    id: editingType.id,
                    payload: { title: values.title },
                });
            } else {
                await createMutation.mutateAsync({ title: values.title });
            }
            message.success('با موفقیت ذخیره شد');
            setFormOpen(false);
        } catch (error) {
            message.error(error.response?.data?.detail || 'ذخیره نوع پست با خطا مواجه شد');
        }
    };

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadJobPostTypesImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'job-post-types-import-template.xlsx';
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (error) {
            message.error(error.response?.data?.detail || 'دانلود فایل قالب با خطا مواجه شد');
        }
    };

    const handleImport = async () => {
        if (!selectedFile) return;
        try {
            const result = await importMutation.mutateAsync(selectedFile);
            setImportOpen(false);
            setFileList([]);
            setSelectedFile(null);
            setImportResult(result);
        } catch (error) {
            message.error(error.response?.data?.detail || 'وارد کردن انواع پست با خطا مواجه شد');
        }
    };

    const columns = [
        { title: 'عنوان نوع پست', dataIndex: 'title', key: 'title' },
        {
            title: 'عملیات',
            key: 'actions',
            render: (_, jobPostType) => (
                <Button
                    icon={<EditOutlined />}
                    size="small"
                    onClick={() => openEdit(jobPostType)}
                >
                    ویرایش
                </Button>
            ),
        },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>انواع پست</Title>
                <Space wrap>
                    <Button icon={<UploadOutlined />} onClick={() => setImportOpen(true)}>
                        وارد کردن انواع پست
                    </Button>
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                        افزودن نوع پست
                    </Button>
                </Space>
            </div>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={jobPostTypes}
                loading={isLoading}
                pagination={{ pageSize: 10, showSizeChanger: false }}
            />

            <Modal
                title={editingType ? 'ویرایش نوع پست' : 'افزودن نوع پست'}
                open={formOpen}
                onCancel={() => {
                    if (!formPending) setFormOpen(false);
                }}
                onOk={() => form.submit()}
                confirmLoading={formPending}
                okText="ذخیره"
                cancelText="انصراف"
                closable={!formPending}
                maskClosable={!formPending}
                destroyOnHidden
            >
                <Form form={form} layout="vertical" onFinish={handleFinish}>
                    {editingType && (
                        <Form.Item name="id" label="شناسه">
                            <Input disabled />
                        </Form.Item>
                    )}
                    <Form.Item
                        name="title"
                        label="عنوان نوع پست"
                        rules={[{ required: true, whitespace: true, message: 'عنوان را وارد کنید' }]}
                    >
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title="وارد کردن انواع پست"
                open={importOpen}
                onCancel={() => {
                    if (importMutation.isPending) return;
                    setImportOpen(false);
                    setFileList([]);
                    setSelectedFile(null);
                }}
                onOk={handleImport}
                okText="بارگذاری"
                cancelText="انصراف"
                okButtonProps={{ disabled: !selectedFile }}
                confirmLoading={importMutation.isPending}
                closable={!importMutation.isPending}
                maskClosable={!importMutation.isPending}
            >
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                    <Button icon={<DownloadOutlined />} onClick={handleDownloadTemplate}>
                        دانلود فایل قالب
                    </Button>
                    <Upload
                        accept=".xlsx"
                        fileList={fileList}
                        maxCount={1}
                        disabled={importMutation.isPending}
                        beforeUpload={() => false}
                        onChange={({ fileList: nextFileList }) => {
                            const next = nextFileList.slice(-1);
                            setFileList(next);
                            setSelectedFile(next[0]?.originFileObj || null);
                        }}
                        onRemove={() => {
                            setSelectedFile(null);
                            setFileList([]);
                        }}
                    >
                        <Button icon={<UploadOutlined />} disabled={importMutation.isPending}>
                            انتخاب فایل Excel
                        </Button>
                    </Upload>
                </Space>
            </Modal>

            <Modal
                title="نتیجه وارد کردن انواع پست"
                open={Boolean(importResult)}
                onCancel={() => setImportResult(null)}
                footer={null}
            >
                {importResult && (
                    <>
                        <p>ایجادشده: {importResult.created}</p>
                        <p>ردشده به دلیل تکراری بودن: {importResult.skippedExisting}</p>
                        <p>ناموفق: {importResult.failed}</p>
                        {importResult.errors.length > 0 && (
                            <List
                                size="small"
                                bordered
                                dataSource={importResult.errors}
                                renderItem={(error) => (
                                    <List.Item>ردیف {error.rowNumber}: {error.reason}</List.Item>
                                )}
                                style={{ maxHeight: 280, overflowY: 'auto' }}
                            />
                        )}
                    </>
                )}
            </Modal>
        </div>
    );
}
