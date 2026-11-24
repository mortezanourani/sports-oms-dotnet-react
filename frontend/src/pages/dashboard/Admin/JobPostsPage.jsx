import { useState } from 'react';
import {
    Button, Form, Input, List, Modal, Select, Space, Table, Typography, Upload, message,
} from 'antd';
import {
    DownloadOutlined, EditOutlined, PlusOutlined, UploadOutlined,
} from '@ant-design/icons';
import { downloadJobPostsImportTemplate } from '../../../api/jobPosts';
import { useOrgUnits } from '../../../hooks/useOrgUnits';
import { useJobPostTypes } from '../../../hooks/useJobPostTypes';
import {
    useCreateJobPost, useImportJobPosts, useJobPosts, useUpdateJobPost,
} from '../../../hooks/useJobPosts';

const { Title, Text } = Typography;

const MANAGER_TYPE_OPTIONS = [
    { value: 'Unspecified', label: 'نامشخص' },
    { value: 'BasicManager', label: 'مدیر پایه' },
    { value: 'MiddleManager', label: 'مدیر میانی' },
    { value: 'SeniorManager', label: 'مدیر ارشد' },
];

const BREAK_OUT_OPTIONS = [
    { value: 'true', label: 'بله' },
    { value: 'false', label: 'خیر' },
    { value: 'null', label: 'نامشخص' },
];

const MANAGER_TYPE_LABELS = Object.fromEntries(
    MANAGER_TYPE_OPTIONS.map(({ value, label }) => [value, label]),
);

export default function JobPostsPage() {
    const { data: jobPosts = [], isLoading } = useJobPosts();
    const { data: jobPostTypes = [] } = useJobPostTypes();
    const { data: orgUnits = [] } = useOrgUnits();
    const [form] = Form.useForm();
    const [formOpen, setFormOpen] = useState(false);
    const [importOpen, setImportOpen] = useState(false);
    const [editingPost, setEditingPost] = useState(null);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);

    const createMutation = useCreateJobPost();
    const updateMutation = useUpdateJobPost();
    const importMutation = useImportJobPosts();
    const formPending = createMutation.isPending || updateMutation.isPending;
    const jobPostTypeTitles = Object.fromEntries(
        jobPostTypes.map((jobPostType) => [jobPostType.id, jobPostType.title]),
    );
    const orgUnitTitles = Object.fromEntries(
        orgUnits.map((orgUnit) => [orgUnit.id, orgUnit.title]),
    );

    const openCreate = () => {
        setEditingPost(null);
        form.resetFields();
        form.setFieldsValue({ breakOut: 'null' });
        setFormOpen(true);
    };

    const openEdit = (jobPost) => {
        setEditingPost(jobPost);
        form.setFieldsValue({
            id: jobPost.id,
            jobPostTypeId: jobPost.jobPostTypeId,
            orgUnitId: jobPost.orgUnitId,
            typeCode: jobPost.typeCode,
            title: jobPost.title,
            managerType: jobPost.managerType,
            breakOut: jobPost.breakOut === null ? 'null' : String(jobPost.breakOut),
        });
        setFormOpen(true);
    };

    const handleFinish = async (values) => {
        const payload = {
            jobPostTypeId: values.jobPostTypeId,
            orgUnitId: values.orgUnitId,
            typeCode: values.typeCode,
            title: values.title,
            managerType: values.managerType,
            breakOut: values.breakOut === 'true'
                ? true
                : values.breakOut === 'false'
                    ? false
                    : null,
        };
        try {
            if (editingPost) {
                await updateMutation.mutateAsync({ id: editingPost.id, payload });
            } else {
                await createMutation.mutateAsync(payload);
            }
            message.success('با موفقیت ذخیره شد');
            setFormOpen(false);
        } catch (error) {
            message.error(error.response?.data?.detail || 'ذخیره پست سازمانی با خطا مواجه شد');
        }
    };

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadJobPostsImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'job-posts-import-template.xlsx';
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
            message.error(error.response?.data?.detail || 'وارد کردن پست‌ها با خطا مواجه شد');
        }
    };

    const columns = [
        { title: 'عنوان پست', dataIndex: 'title', key: 'title' },
        {
            title: 'نوع پست',
            dataIndex: 'jobPostTypeId',
            key: 'jobPostTypeId',
            render: (id) => jobPostTypeTitles[id] || id,
        },
        {
            title: 'واحد سازمانی',
            dataIndex: 'orgUnitId',
            key: 'orgUnitId',
            render: (id) => orgUnitTitles[id] || id,
        },
        { title: 'کد نوع پست', dataIndex: 'typeCode', key: 'typeCode' },
        {
            title: 'نوع مدیر حرفه‌ای',
            dataIndex: 'managerType',
            key: 'managerType',
            render: (type) => MANAGER_TYPE_LABELS[type] || type,
        },
        {
            title: 'شکسته‌شده',
            dataIndex: 'breakOut',
            key: 'breakOut',
            render: (value) => value === null ? 'نامشخص' : value ? 'بله' : 'خیر',
        },
        {
            title: 'عملیات',
            key: 'actions',
            render: (_, jobPost) => (
                <Button
                    icon={<EditOutlined />}
                    size="small"
                    onClick={() => openEdit(jobPost)}
                >
                    ویرایش
                </Button>
            ),
        },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>پست‌های سازمانی</Title>
                <Space wrap>
                    <Button icon={<UploadOutlined />} onClick={() => setImportOpen(true)}>
                        وارد کردن پست‌ها
                    </Button>
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                        افزودن پست سازمانی
                    </Button>
                </Space>
            </div>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={jobPosts}
                loading={isLoading}
                pagination={{ pageSize: 10, showSizeChanger: false }}
                scroll={{ x: true }}
            />

            <Modal
                title={editingPost ? 'ویرایش پست سازمانی' : 'افزودن پست سازمانی'}
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
                    {editingPost && (
                        <Form.Item name="id" label="شناسه">
                            <Input disabled />
                        </Form.Item>
                    )}
                    <Form.Item
                        name="jobPostTypeId"
                        label="نوع پست"
                        rules={[{ required: true, message: 'نوع پست را انتخاب کنید' }]}
                    >
                        <Select
                            showSearch
                            optionFilterProp="label"
                            options={jobPostTypes.map((type) => ({
                                value: type.id,
                                label: type.title,
                            }))}
                        />
                    </Form.Item>
                    <Form.Item
                        name="orgUnitId"
                        label="واحد سازمانی"
                        rules={[{ required: true, message: 'واحد سازمانی را انتخاب کنید' }]}
                    >
                        <Select
                            showSearch
                            optionFilterProp="label"
                            options={orgUnits.map((unit) => ({
                                value: unit.id,
                                label: unit.title,
                            }))}
                        />
                    </Form.Item>
                    <Form.Item
                        name="typeCode"
                        label="کد نوع پست"
                        rules={[{ required: true, whitespace: true, message: 'کد نوع پست را وارد کنید' }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item
                        name="title"
                        label="عنوان پست"
                        rules={[
                            { required: true, whitespace: true, message: 'عنوان پست را وارد کنید' },
                            { max: 160, message: 'عنوان پست باید حداکثر ۱۶۰ نویسه باشد' },
                        ]}
                    >
                        <Input maxLength={160} />
                    </Form.Item>
                    <Form.Item
                        name="managerType"
                        label="نوع مدیر حرفه‌ای"
                        rules={[{ required: true, message: 'نوع مدیر حرفه‌ای را انتخاب کنید' }]}
                    >
                        <Select options={MANAGER_TYPE_OPTIONS} />
                    </Form.Item>
                    <Form.Item name="breakOut" label="شکسته‌شده">
                        <Select options={BREAK_OUT_OPTIONS} />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title="وارد کردن پست‌های سازمانی"
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
                    <Text>
                        نوع پست و واحد سازمانی را با عنوان ثبت‌شده وارد کنید. نوع مدیر حرفه‌ای می‌تواند نامشخص،
                        مدیر پایه، مدیر میانی یا مدیر ارشد باشد؛ مقدار شکسته‌شده می‌تواند true، false یا خالی باشد.
                    </Text>
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
                title="نتیجه وارد کردن پست‌ها"
                open={Boolean(importResult)}
                onCancel={() => setImportResult(null)}
                footer={null}
            >
                {importResult && (
                    <>
                        <p>ایجادشده: {importResult.created}</p>
                        <p>ردشده به دلیل تکراری بودن عنوان: {importResult.skippedExisting}</p>
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
