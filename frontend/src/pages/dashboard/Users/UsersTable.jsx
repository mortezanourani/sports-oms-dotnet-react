import { useState, useEffect } from 'react';
import {
  Table, Button, Space, Popconfirm, message, Modal, Form,
  Input, Select, Tag,
} from 'antd';
import { PlusOutlined, EditOutlined, StopOutlined, KeyOutlined } from '@ant-design/icons';
import {
  useUsers, useUser, useCreateUser, useUpdateUser, useDeactivateUser, useResetUserPassword,
} from '../../../hooks/useUsers';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { useAuth } from '../../../hooks/useAuth';
import { ROLE_LABELS, SCOPE_LEVEL_LABELS } from '../../../constants/labels';
import {useGeoSections} from "../../../hooks/useGeoSections.js";

const ROLE_PRIORITY = {
  Admin: 4,
  Manager: 3,
  HumanResourceManager: 2,
  HumanResourceOfficer: 1,
};

const SCOPE_PRIORITY = {
  Mastery: 2,
  Province: 1,
  City: 0,
};

export default function UsersList({ filters }) {
  const geo = useGeoSections();

  const { role: myRole, scopeLevel: myScopeLevel } = useAuth();
  const isAdmin = ['Admin'].includes(myRole);
  const canManage = ['Admin', 'Manager', 'HumanResourceManager', 'HumanResourceOfficer'].includes(myRole);
  // Manager can view this page (matches the backend's [Authorize(Roles =
  // "Admin,Manager")] on List/Get), but every write action is Admin-only
  // per the role table ("Manager ... can't create users and can't assign
  // any presidents/vices") — isAdmin gates every button below so a
  // Manager never sees controls that would just fail with a 403 anyway.

  const [modalOpen, setModalOpen] = useState(false);
  const [passwordModalOpen, setPasswordModalOpen] = useState(false);
  const [passwordUser, setPasswordUser] = useState(null);
  const [editingId, setEditingId] = useState(null);
  const [scopeLevel, setScopeLevel] = useState('Mastery');
  const [selectedProvinceId, setSelectedProvinceId] = useState(null);
  const [form] = Form.useForm();
  const [passwordForm] = Form.useForm();

  const { data: users, isLoading } = useUsers(filters);
  const { data: editingUser, isFetching: isFetchingEditing } = useUser(editingId);
  const { data: provinces } = useProvinces();
  const { data: cities } = useCities(selectedProvinceId);

  const createMutation = useCreateUser();
  const updateMutation = useUpdateUser();
  const deactivateMutation = useDeactivateUser();
  const resetPasswordMutation = useResetUserPassword();

  const isEdit = Boolean(editingId);

  useEffect(() => {
    if (editingId && editingUser) {
      form.setFieldsValue(editingUser);
      setScopeLevel(editingUser.scopeLevel);
      setSelectedProvinceId(editingUser.scopeProvinceId);
    }
  }, [editingId, editingUser, form]);

  const openCreate = () => {
    setEditingId(null);
    setScopeLevel('Mastery');
    setSelectedProvinceId(null);
    form.resetFields();
    setModalOpen(true);
  };

  const openEdit = (user) => {
    setEditingId(user.id);
    setModalOpen(true);
  };

  const handleFinish = async (values) => {
    // Mirrors the backend's own normalization: fields that don't apply to
    // the selected scope level are explicitly cleared before sending,
    // rather than relying on the backend to silently ignore them — this
    // keeps the payload honest about what will actually be saved, even
    // though ValidateScopeAsync on the backend would catch a mismatch anyway.
    const payload = {
      ...values,
      scopeProvinceId: scopeLevel === 'Mastery' ? null : values.scopeProvinceId,
      scopeCityId: scopeLevel === 'City' ? values.scopeCityId : null,
    };

    try {
      if (isEdit) {
        await updateMutation.mutateAsync({ id: editingId, payload });
      } else {
        await createMutation.mutateAsync(payload);
      }
      message.success('با موفقیت ذخیره شد');
      setModalOpen(false);
    } catch (err) {
      message.error(err.response?.data?.detail || 'خطایی رخ داد');
    }
  };

  const handleDeactivate = async (id) => {
    try {
      await deactivateMutation.mutateAsync(id);
      message.success('کاربر غیرفعال شد');
    } catch (err) {
      message.error(err.response?.data?.detail || 'خطا در غیرفعال‌سازی');
    }
  };

  const openPasswordModal = (user) => {
    setPasswordUser(user);
    passwordForm.resetFields();
    setPasswordModalOpen(true);
  };

  const handlePasswordReset = async (values) => {
    try {
      await resetPasswordMutation.mutateAsync({
        id: passwordUser.id,
        newPassword: values.newPassword,
      });
      message.success('رمز عبور کاربر با موفقیت تغییر کرد');
      setPasswordModalOpen(false);
      setPasswordUser(null);
      passwordForm.resetFields();
    } catch (err) {
      message.error(err.response?.data?.detail || 'تغییر رمز عبور کاربر با خطا مواجه شد');
    }
  };

  const hasHigherPriority = (user) =>
    (ROLE_PRIORITY[myRole] ?? 0) > (ROLE_PRIORITY[user.role] ?? 0) ||
    (SCOPE_PRIORITY[myScopeLevel] ?? 0) > (SCOPE_PRIORITY[user.scopeLevel] ?? 0);

  const columns = [
    { title: 'نام کاربری', dataIndex: 'username' },
    { title: 'نام', dataIndex: 'firstName' },
    { title: 'نام خانوادگی', dataIndex: 'lastName' },
    { title: 'نقش', dataIndex: 'role', render: (v) => <Tag>{ROLE_LABELS[v] || v}</Tag> },
    /*{ title: 'سطح دسترسی', dataIndex: 'scopeLevel', render: (v) => SCOPE_LEVEL_LABELS[v] || v },*/
    { title: 'استان', dataIndex: 'scopeProvinceName', render: (v) => v || '—' },
    { title: 'شهر', dataIndex: 'scopeCityName', render: (v) => v || '—' },
    {
      title: 'عملیات', key: 'actions',
      render: (_, record) => (
        <Space>
          {canManage && (
            <>
              {hasHigherPriority(record) && (
                <Button
                  icon={<KeyOutlined />}
                  size="small"
                  title="تغییر رمز عبور"
                  onClick={() => openPasswordModal(record)}
                />
              )}
              <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)} />
              <Popconfirm title="این کاربر غیرفعال شود؟" onConfirm={() => handleDeactivate(record.id)} okText="بله" cancelText="خیر">
                <Button icon={<StopOutlined />} size="small" danger />
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        {canManage && (
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>افزودن کاربر جدید</Button>
        )}
      </div>

      <Table rowKey="id" columns={columns} dataSource={users} loading={isLoading} pagination={false} />

      <Modal
        title={isEdit ? 'ویرایش کاربر' : 'افزودن کاربر جدید'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        okText="ذخیره"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={handleFinish} disabled={isFetchingEditing}>
          <Form.Item name="username" label="کد ملی" rules={[{ required: true }]}>
            <Input disabled={isEdit} />
          </Form.Item>
          {!isEdit && (
            <Form.Item name="password" label="رمز عبور" rules={[{ required: true, min: 6 }]}>
              <Input.Password />
            </Form.Item>
          )}
          <Space style={{ width: '100%' }} size={"middle"}>
            <Form.Item name="firstName" label="نام" rules={[{ required: true }]} style={{ width: '228px' }}>
              <Input />
            </Form.Item>
            <Form.Item name="lastName" label="نام خانوادگی" rules={[{ required: true }]} style={{ width: '228px' }}>
              <Input />
            </Form.Item>
          </Space>
          <Form.Item name="role" label="نقش" rules={[{ required: true }]}>
            <Select options={Object.entries(ROLE_LABELS)
                .filter(([value]) => value !== 'FederationRep' && (isAdmin || value !== 'Admin'))
                .map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item name="scopeLevel" label="سطح دسترسی جغرافیایی" rules={[{ required: true }]}>
            <Select
              options={Object.entries(SCOPE_LEVEL_LABELS)
                  .filter(([value]) => geo.provinceLocked ? value !== 'Mastery' : true)
                  .map(([value, label]) => ({ value, label }))}
              onChange={(val) => {
                setScopeLevel(val);
                form.setFieldValue('scopeProvinceId', undefined);
                form.setFieldValue('scopeCityId', undefined);
                setSelectedProvinceId(null);
              }}
            />
          </Form.Item>

          {/* Conditional fields — only rendered when relevant to the
              selected scope level, rather than shown-but-disabled, since
              a Mastery-scope user having a visible-but-greyed-out
              province field would be more confusing than just not
              showing it at all. */}
          {(scopeLevel === 'Province' || scopeLevel === 'City') && (
            <Form.Item
              name="scopeProvinceId" label="استان"
              rules={[{ required: true, message: 'انتخاب استان الزامی است' }]}
            >
              <Select
                options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
                onChange={(val) => {
                  setSelectedProvinceId(val);
                  form.setFieldValue('scopeCityId', undefined);
                }}
              />
            </Form.Item>
          )}
          {scopeLevel === 'City' && (
            <Form.Item
              name="scopeCityId" label="شهر"
              rules={[{ required: true, message: 'انتخاب شهر الزامی است' }]}
            >
              <Select disabled={!selectedProvinceId} options={cities?.map((c) => ({ value: c.id, label: c.name }))} />
            </Form.Item>
          )}
        </Form>
      </Modal>

      <Modal
        title={`تغییر رمز عبور کاربر ${passwordUser?.username || ''}`}
        open={passwordModalOpen}
        onCancel={() => {
          if (resetPasswordMutation.isPending) return;
          setPasswordModalOpen(false);
          setPasswordUser(null);
          passwordForm.resetFields();
        }}
        onOk={() => passwordForm.submit()}
        confirmLoading={resetPasswordMutation.isPending}
        okText="ذخیره"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={passwordForm} layout="vertical" onFinish={handlePasswordReset}>
          <Form.Item
            name="newPassword"
            label="رمز عبور جدید"
            rules={[{ required: true, min: 6, message: 'رمز عبور باید حداقل ۶ نویسه باشد' }]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label="تکرار رمز عبور جدید"
            dependencies={['newPassword']}
            rules={[
              { required: true, message: 'تکرار رمز عبور جدید الزامی است' },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error('رمزهای عبور جدید یکسان نیستند'));
                },
              }),
            ]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
