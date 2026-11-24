import { useState } from 'react';
import {
    Button, Form, Input, List, Modal, Select, Space, Table, Typography, Upload, message,
} from 'antd';
import {
    DownloadOutlined, EditOutlined, PlusOutlined, UploadOutlined,
} from '@ant-design/icons';
import { downloadOrgUnitsImportTemplate } from '../../../api/orgUnits';
import {
    useCreateOrgUnit, useImportOrgUnits, useOrgUnits, useUpdateOrgUnit,
} from '../../../hooks/useOrgUnits';

const { Title } = Typography;

const TYPE_OPTIONS = [
    { value: 'GeneralAdministration', label: 'اداره کل' },
    { value: 'Administration', label: 'اداره' },
    { value: 'Group', label: 'گروه' },
    { value: 'Representation', label: 'نمایندگی' },
    { value: 'SportsComplex', label: 'مجموعه ورزشی' },
];

const SCOPE_OPTIONS = [
    { value: 'Provincial', label: 'استانی' },
    { value: 'City', label: 'شهرستانی' },
];

const TYPE_LABELS = Object.fromEntries(TYPE_OPTIONS.map(({ value, label }) => [value, label]));
const SCOPE_LABELS = Object.fromEntries(SCOPE_OPTIONS.map(({ value, label }) => [value, label]));

export default function OrgUnitsPage() {
    const { data: orgUnits = [], isLoading } = useOrgUnits();
    const [form] = Form.useForm();
    const [formOpen, setFormOpen] = useState(false);
    const [importOpen, setImportOpen] = useState(false);
    const [editingUnit, setEditingUnit] = useState(null);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);

    const createMutation = useCreateOrgUnit();
    const updateMutation = useUpdateOrgUnit();
    const importMutation = useImportOrgUnits();
    const formPending = createMutation.isPending || updateMutation.isPending;

    const getDescendantIds = (unitId) => {
        const descendants = new Set([unitId]);
        let changed = true;
        while (changed) {
            changed = false;
            orgUnits.forEach((unit) => {
                if (unit.parentOrgUnitId && descendants.has(unit.parentOrgUnitId)
                    && !descendants.has(unit.id)) {
                    descendants.add(unit.id);
                    changed = true;
                }
            });
        }
        return descendants;
    };

    const unavailableParentIds = editingUnit
        ? getDescendantIds(editingUnit.id)
        : new Set();
    const parentOptions = [
        { value: '', label: 'بدون والد' },
        ...orgUnits
            .filter((unit) => !unavailableParentIds.has(unit.id))
            .map((unit) => ({ value: unit.id, label: unit.title })),
    ];

    const openCreate = () => {
        setEditingUnit(null);
        form.resetFields();
        form.setFieldsValue({ parentOrgUnitId: '' });
        setFormOpen(true);
    };

    const openEdit = (unit) => {
        setEditingUnit(unit);
        form.setFieldsValue({
            id: unit.id,
            title: unit.title,
            type: unit.type,
            scope: unit.scope,
            parentOrgUnitId: unit.parentOrgUnitId || '',
        });
        setFormOpen(true);
    };

    const handleFinish = async (values) => {
        const payload = {
            title: values.title,
            type: values.type,
            scope: values.scope,
            parentOrgUnitId: values.parentOrgUnitId || null,
        };
        try {
            if (editingUnit) {
                await updateMutation.mutateAsync({ id: editingUnit.id, payload });
            } else {
                await createMutation.mutateAsync(payload);
            }
            message.success('با موفقیت ذخیره شد');
            setFormOpen(false);
        } catch (error) {
            message.error(error.response?.data?.detail || 'ذخیره واحد سازمانی با خطا مواجه شد');
        }
    };

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadOrgUnitsImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'org-units-import-template.xlsx';
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
            message.error(error.response?.data?.detail || 'وارد کردن واحدهای سازمانی با خطا مواجه شد');
        }
    };

    const columns = [
        { title: 'عنوان واحد سازمانی', dataIndex: 'title', key: 'title' },
        {
            title: 'نوع واحد سازمانی',
            dataIndex: 'type',
            key: 'type',
            render: (type) => TYPE_LABELS[type] || type,
        },
        {
            title: 'دامنه',
            dataIndex: 'scope',
            key: 'scope',
            render: (scope) => SCOPE_LABELS[scope] || scope,
        },
        { title: 'واحد سازمانی والد', dataIndex: 'parentOrgUnitTitle', key: 'parentOrgUnitTitle' },
        {
            title: 'عملیات',
            key: 'actions',
            render: (_, unit) => (
                <Button
                    icon={<EditOutlined />}
                    size="small"
                    onClick={() => openEdit(unit)}
                >
                    ویرایش
                </Button>
            ),
        },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>واحدهای سازمانی</Title>
                <Space wrap>
                    <Button icon={<UploadOutlined />} onClick={() => setImportOpen(true)}>
                        وارد کردن واحدها
                    </Button>
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                        افزودن واحد سازمانی
                    </Button>
                </Space>
            </div>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={orgUnits}
                loading={isLoading}
                pagination={{ pageSize: 10, showSizeChanger: false }}
            />

            <Modal
                title={editingUnit ? 'ویرایش واحد سازمانی' : 'افزودن واحد سازمانی'}
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
                    <Form.Item
                        name="title"
                        label="عنوان واحد سازمانی"
                        rules={[{ required: true, whitespace: true, message: 'عنوان را وارد کنید' }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item
                        name="type"
                        label="نوع واحد سازمانی"
                        rules={[{ required: true, message: 'نوع واحد سازمانی را انتخاب کنید' }]}
                    >
                        <Select options={TYPE_OPTIONS} />
                    </Form.Item>
                    <Form.Item
                        name="scope"
                        label="دامنه"
                        rules={[{ required: true, message: 'دامنه را انتخاب کنید' }]}
                    >
                        <Select options={SCOPE_OPTIONS} />
                    </Form.Item>
                    <Form.Item name="parentOrgUnitId" label="واحد سازمانی والد">
                        <Select
                            showSearch
                            optionFilterProp="label"
                            options={parentOptions}
                        />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title="وارد کردن واحدهای سازمانی"
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
                title="نتیجه وارد کردن واحدهای سازمانی"
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
