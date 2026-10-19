import { Select, Typography, Button, Space, Upload, Modal, List, message, Divider } from 'antd';
import { useState } from 'react';
import { DownloadOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import { downloadFederationsImportTemplate } from '../../../api/federations';
import { useImportFederations } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { useGeoSections } from '../../../hooks/useGeoSections';
import { canManageCity, canManageMastery, canManageProvincial } from '../../../utils/federationPermissions';
import FederationMasteryTable from './FederationMasteryTable';
import FederationsFlatTable from './FederationsFlatTable';
import FederationCreateModal from './FederationCreateModal';

const { Title } = Typography;

export default function FederationsList() {
    const geo = useGeoSections();
    const { role } = useAuth();
    const canImport = ['Admin', 'Manager', 'SportsManager', 'SportsOfficer'].includes(role);
    const [createOpen, setCreateOpen] = useState(false);
    const [importModalOpen, setImportModalOpen] = useState(false);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);
    const importMutation = useImportFederations();

    const canCreateFederation =
        canManageMastery(geo.auth) ||
        canManageProvincial(geo.auth, geo.auth.scopeProvinceId) ||
        canManageCity(geo.auth, geo.auth.scopeCityId, geo.auth.scopeProvinceId);
    const provinceId = geo.auth.scopeLevel === 'Province'
        ? geo.auth.scopeProvinceId
        : geo.selectedProvinceId;
    const cityId = geo.auth.scopeLevel === 'City'
        ? geo.auth.scopeCityId
        : geo.selectedCityId;

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadFederationsImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'federations-import-template.xlsx';
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (error) {
            message.error(error.response?.data?.detail || 'دانلود فایل نمونه با خطا مواجه شد');
        }
    };

    const handleImport = async () => {
        if (!selectedFile) return;
        try {
            const result = await importMutation.mutateAsync(selectedFile);
            setImportModalOpen(false);
            setFileList([]);
            setSelectedFile(null);
            setImportResult(result);
        } catch (error) {
            message.error(error.response?.data?.detail || 'وارد کردن فدراسیون‌ها با خطا مواجه شد');
        }
    };

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>فدراسیون‌ها</Title>
                <Space>
                    {canCreateFederation && (
                        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                            افزودن فدراسیون
                        </Button>
                    )}
                    {canImport && (
                        <Button icon={<UploadOutlined />} onClick={() => setImportModalOpen(true)}>
                            وارد کردن فدراسیون‌ها
                        </Button>
                    )}
                </Space>
            </div>

            {geo.showMastery && (
                <>
                    <Title level={5}>فدراسیون‌های کشوری</Title>
                    <FederationMasteryTable />
                    <Divider />
                </>
            )}

            {geo.showProvinceSection && (
                <>
                    <Title level={5}>فدراسیون‌های استانی</Title>
                    {geo.showMastery && (
                        <Select
                            allowClear
                            placeholder="همه استان‌ها"
                            style={{ width: 240, marginBottom: 16 }}
                            value={geo.selectedProvinceId || undefined}
                            onChange={(selectedId) => {
                                geo.setSelectedProvinceId(selectedId || null);
                                geo.setSelectedCityId(null);
                            }}
                            options={geo.provinceOptions?.map((province) => ({
                                value: province.id,
                                label: province.name,
                            }))}
                        />
                    )}
                    <FederationsFlatTable level="Provincial" provinceId={provinceId} />
                    <Divider />
                </>
            )}

            <Title level={5}>فدراسیون‌های شهری</Title>
            {geo.auth.scopeLevel !== 'City' && (
                <Select
                    allowClear
                    placeholder="همه شهرها"
                    style={{ width: 240, marginBottom: 16 }}
                    disabled={!provinceId}
                    value={geo.selectedCityId || undefined}
                    onChange={(selectedId) => geo.setSelectedCityId(selectedId || null)}
                    options={geo.cityOptions?.map((city) => ({
                        value: city.id,
                        label: geo.auth.scopeLevel === 'Mastery'
                            ? `${city.name} - ${city.provinceName}`
                            : city.name,
                    }))}
                />
            )}
            <FederationsFlatTable
                level="City"
                provinceId={provinceId}
                cityId={cityId}
            />

            <FederationCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />
            <Modal
                title="وارد کردن فدراسیون‌ها"
                open={importModalOpen}
                onCancel={() => {
                    if (importMutation.isPending) return;
                    setImportModalOpen(false);
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
                title="نتیجه وارد کردن فدراسیون‌ها"
                open={Boolean(importResult)}
                onCancel={() => setImportResult(null)}
                footer={null}
            >
                {importResult && (
                    <>
                        <p>فدراسیون‌های ایجادشده: {importResult.createdFederations}</p>
                        <p>فدراسیون‌های از قبل موجود: {importResult.skippedExistingFederations}</p>
                        <p>حساب نماینده ایجادشده: {importResult.createdRepAccounts}</p>
                        <p>سمت‌های اختصاص‌یافته: {importResult.assignedPositions}</p>
                        <p>انتصاب‌های نماینده ردشده: {importResult.skippedRepAssignments}</p>
                        <p>ردشده به دلیل داده نامعتبر: {importResult.skippedInvalid}</p>
                        <p>ردشده به دلیل خارج بودن از محدوده دسترسی: {importResult.skippedOutOfScope}</p>
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
