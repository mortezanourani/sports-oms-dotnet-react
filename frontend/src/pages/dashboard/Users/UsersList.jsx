import { useState } from 'react';
import { Tabs, Select, Typography, Button, Space, Upload, Modal, List, message } from 'antd';
import { DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import { downloadUsersImportTemplate } from '../../../api/users';
import { useImportUsers } from '../../../hooks/useUsers';
import { useAuth } from '../../../hooks/useAuth';
import { useGeoSections } from '../../../hooks/useGeoSections';
import UsersTable from './UsersTable';

const { Title } = Typography;

export default function UsersList() {
  const geo = useGeoSections();
  const { role: myRole } = useAuth();
  const canManage = ['Admin', 'Manager', 'HumanResourceManager', 'HumanResourceOfficer'].includes(myRole);
  const [importResult, setImportResult] = useState(null);
  const importMutation = useImportUsers();

  const handleDownloadTemplate = async () => {
    try {
      const blob = await downloadUsersImportTemplate();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'users-import-template.xlsx';
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) {
      message.error(error.response?.data?.detail || 'دانلود فایل نمونه با خطا مواجه شد');
    }
  };

  const handleImport = async ({ file }) => {
    try {
      const result = await importMutation.mutateAsync(file);
      setImportResult(result);
    } catch (error) {
      message.error(error.response?.data?.detail || 'وارد کردن کاربران با خطا مواجه شد');
    }
  };

  const items = [
    geo.showMastery && {
      key: 'mastery', label: 'وزارت',
      children: <UsersTable filters={{ scopeLevel: 'Mastery' }} />,
    },
    geo.showProvinceSection && {
      key: 'province', label: 'اداره کل',
      children: (
          <>
            {/* Hidden entirely (not just disabled) for a Province-scoped
              viewer, matching the City tab's existing pattern — they
              already know which province they're looking at. */}
            {!geo.provinceLocked && (
                <Select
                    placeholder="انتخاب استان" style={{ width: 220, marginBottom: 16 }}
                    value={geo.selectedProvinceId}
                    onChange={geo.setSelectedProvinceId}
                    options={geo.provinceOptions?.map((p) => ({ value: p.id, label: p.name }))}
                />
            )}
            <UsersTable filters={{ scopeLevel: 'Province', provinceId: geo.selectedProvinceId }} />
          </>
      ),
    },
    {
      key: 'city', label: 'شهرستان',
      children: (
          <>
            {!geo.cityLocked && (
                <Select
                    placeholder="ابتدا استان را در بخش «استان» انتخاب کنید"
                    style={{ width: 220, marginBottom: 16 }}
                    disabled={!geo.selectedProvinceId}
                    value={geo.selectedCityId}
                    onChange={geo.setSelectedCityId}
                    options={geo.cityOptions?.map((c) => ({ value: c.id, label: c.name }))}
                />
            )}
            <UsersTable filters={{
              scopeLevel: 'City',
              cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
            }} />
          </>
      ),
    },
  ].filter(Boolean);

  return (
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>کاربران</Title>
          {canManage && (
            <Space>
              <Button icon={<DownloadOutlined />} onClick={handleDownloadTemplate}>دانلود قالب</Button>
              <Upload
                accept=".xlsx"
                disabled={importMutation.isPending}
                showUploadList={false}
                maxCount={1}
                beforeUpload={(file) => {
                  void handleImport({ file });
                  return false;
                }}
              >
                <Button icon={<UploadOutlined />} loading={importMutation.isPending}>وارد کردن کاربران</Button>
              </Upload>
            </Space>
          )}
        </div>
        <Tabs items={items} />
        <Modal
          title="نتیجه وارد کردن کاربران"
          open={Boolean(importResult)}
          onCancel={() => setImportResult(null)}
          footer={null}
        >
          {importResult && (
            <>
              <p>ایجادشده: {importResult.created}</p>
              <p>نام کاربری تکراری و ردشده: {importResult.skippedExisting}</p>
              <p>ردشده به دلیل فیلدهای الزامی خالی: {importResult.skippedRequired}</p>
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
