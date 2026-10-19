import { Select, Typography, Button, Space, Upload, Modal, List, message } from 'antd';
import { useState } from 'react';
import { DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import { downloadChampionsImportTemplate } from '../../../api/champions';
import { useChampionFederations, useImportChampions } from '../../../hooks/useChampions';
import { useAuth } from '../../../hooks/useAuth';
import { useGeoSections } from '../../../hooks/useGeoSections';
import ChampionsTable from './ChampionsTable';

const { Title } = Typography;

export default function ChampionsList() {
    const geo = useGeoSections();
    const { role } = useAuth();
    const canImport = ['Admin', 'Manager', 'SportsManager', 'SportsOfficer'].includes(role);
    const [importModalOpen, setImportModalOpen] = useState(false);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);
    const importMutation = useImportChampions();
    const [selectedFederationId, setSelectedFederationId] = useState(null);
    const { data: championFederations } = useChampionFederations();

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadChampionsImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'champions-import-template.xlsx';
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
            message.error(error.response?.data?.detail || 'وارد کردن قهرمانان با خطا مواجه شد');
        }
    };

    const isMasteryScope = geo.auth.scopeLevel === 'Mastery';
    const isProvinceScope = geo.auth.scopeLevel === 'Province';
    const filters = {
        federationId: selectedFederationId || undefined,
        ...(isMasteryScope && { provinceId: geo.selectedProvinceId || undefined }),
        ...((isMasteryScope || isProvinceScope) && {
            cityId: geo.selectedCityId || undefined,
        }),
    };

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>قهرمانان</Title>
            </div>
            <ChampionsTable
                key={`${filters.provinceId || ''}:${filters.cityId || ''}:${filters.federationId || ''}`}
                filters={filters}
                filtersToolbar={(
                    <Space wrap>
                        {isMasteryScope && (
                            <Select
                                allowClear
                                placeholder="همه استان‌ها"
                                style={{ width: 220 }}
                                value={geo.selectedProvinceId || undefined}
                                onChange={(provinceId) => {
                                    geo.setSelectedCityId(null);
                                    geo.setSelectedProvinceId(provinceId || null);
                                }}
                                options={geo.provinceOptions?.map((province) => ({
                                    value: province.id,
                                    label: province.name,
                                }))}
                            />
                        )}
                        {(isMasteryScope || isProvinceScope) && (
                            <Select
                                allowClear
                                placeholder="همه شهرها"
                                style={{ width: 220 }}
                                disabled={isMasteryScope && !geo.selectedProvinceId}
                                value={geo.selectedCityId || undefined}
                                onChange={(cityId) => geo.setSelectedCityId(cityId || null)}
                                options={geo.cityOptions?.map((city) => ({
                                    value: city.id,
                                    label: city.name,
                                }))}
                            />
                        )}
                        <Select
                            allowClear
                            placeholder="همه فدراسیون‌ها"
                            style={{ width: 260 }}
                            value={selectedFederationId || undefined}
                            onChange={(federationId) => setSelectedFederationId(federationId || null)}
                            options={championFederations?.map((federation) => ({
                                value: federation.id,
                                label: federation.name,
                            }))}
                        />
                    </Space>
                )}
                headerActions={canImport && (
                    <Button icon={<UploadOutlined />} onClick={() => setImportModalOpen(true)}>
                        وارد کردن قهرمانان
                    </Button>
                )}
            />
            <Modal
                title="وارد کردن قهرمانان و مدال‌ها"
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
                title="نتیجه وارد کردن قهرمانان و مدال‌ها"
                open={Boolean(importResult)}
                onCancel={() => setImportResult(null)}
                footer={null}
            >
                {importResult && (
                    <>
                        <p>قهرمان‌های ایجادشده: {importResult.createdChampions}</p>
                        <p>ردیف‌های قهرمان موجود: {importResult.existingChampions}</p>
                        <p>مدال‌های ایجادشده: {importResult.createdMedals}</p>
                        <p>مدال‌های تکراری: {importResult.skippedExistingMedals}</p>
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
