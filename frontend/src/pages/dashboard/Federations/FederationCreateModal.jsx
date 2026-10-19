import { useEffect } from 'react';
import { Modal, Form, Input, Select, message } from 'antd';
import { useCreateFederation, useFederationParentOptions } from '../../../hooks/useFederations';
import { useAllCities, useProvinces } from '../../../hooks/useLocations';
import { useAuth } from '../../../hooks/useAuth';
import { FEDERATION_CATEGORY_LABELS } from '../../../constants/labels';

export default function FederationCreateModal({ open, onClose }) {
    const [form] = Form.useForm();
    const auth = useAuth();
    const level = Form.useWatch('level', form);
    const provinceId = Form.useWatch('provinceId', form);
    const createMutation = useCreateFederation();
    const { data: provinces } = useProvinces();
    const { data: cities } = useAllCities();
    const { data: nationalFederations } = useFederationParentOptions('National');
    const { data: provincialFederations } = useFederationParentOptions('Provincial', provinceId);

    const initialLevel = auth.scopeLevel === 'Mastery'
        ? 'National'
        : auth.scopeLevel === 'Province'
            ? 'Provincial'
            : 'City';

    useEffect(() => {
        if (!open) return;
        form.resetFields();
        form.setFieldsValue({
            level: initialLevel,
            ...(auth.scopeLevel !== 'Mastery' && { provinceId: auth.scopeProvinceId }),
            ...(auth.scopeLevel === 'City' && { cityId: auth.scopeCityId }),
        });
    }, [open, initialLevel, auth.scopeLevel, auth.scopeProvinceId, auth.scopeCityId, form]);

    const handleFinish = async (values) => {
        let payload;
        if (values.level === 'National') {
            payload = { level: 'National', name: values.name, category: values.category || null };
        } else if (values.level === 'Provincial') {
            payload = {
                level: 'Provincial',
                parentFederationId: values.parentFederationId,
                provinceId: values.provinceId,
            };
        } else {
            payload = {
                level: 'City',
                parentFederationId: values.parentFederationId,
                cityId: values.cityId,
            };
        }

        try {
            await createMutation.mutateAsync(payload);
            message.success('با موفقیت ایجاد شد');
            form.resetFields();
            onClose();
        } catch (error) {
            message.error(error.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const handleLevelChange = () => {
        form.setFieldsValue({
            parentFederationId: undefined,
            provinceId: auth.scopeLevel === 'Mastery' ? undefined : auth.scopeProvinceId,
            cityId: auth.scopeLevel === 'City' ? auth.scopeCityId : undefined,
        });
    };

    const availableCities = auth.scopeLevel === 'City'
        ? cities?.filter((city) => city.id === auth.scopeCityId)
        : auth.scopeLevel === 'Province'
            ? cities?.filter((city) => city.provinceId === auth.scopeProvinceId)
            : cities?.filter((city) => city.provinceId === provinceId);

    return (
        <Modal
            title="افزودن فدراسیون"
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            confirmLoading={createMutation.isPending}
            okText="ایجاد"
            cancelText="انصراف"
            destroyOnClose
        >
            <Form form={form} layout="vertical" onFinish={handleFinish}>
                {auth.scopeLevel !== 'City' && (
                    <Form.Item name="level" label="سطح فدراسیون" rules={[{ required: true }]}>
                        <Select
                            onChange={handleLevelChange}
                            options={[
                                ...(auth.scopeLevel === 'Mastery' ? [{ value: 'National', label: 'کشوری' }] : []),
                                { value: 'Provincial', label: 'استانی' },
                                { value: 'City', label: 'شهری' },
                            ]}
                        />
                    </Form.Item>
                )}
                {auth.scopeLevel === 'City' && (
                    <Form.Item name="level" initialValue="City" hidden>
                        <Input />
                    </Form.Item>
                )}

                {level === 'National' && (
                    <>
                        <Form.Item name="name" label="نام فدراسیون" rules={[{ required: true }]}>
                            <Input />
                        </Form.Item>
                        <Form.Item name="category" label="دسته‌بندی">
                            <Select
                                allowClear
                                placeholder="بدون دسته‌بندی"
                                options={Object.entries(FEDERATION_CATEGORY_LABELS)
                                    .filter(([value]) => value !== 'Unknow')
                                    .map(([value, label]) => ({ value, label }))}
                            />
                        </Form.Item>
                    </>
                )}

                {level === 'Provincial' && (
                    <>
                        <Form.Item name="parentFederationId" label="فدراسیون کشوری" rules={[{ required: true }]}>
                            <Select
                                placeholder="انتخاب رشته"
                                options={nationalFederations?.map((federation) => ({
                                    value: federation.id,
                                    label: federation.displayName,
                                }))}
                            />
                        </Form.Item>
                        {auth.scopeLevel === 'Mastery' ? (
                            <Form.Item name="provinceId" label="استان" rules={[{ required: true }]}>
                                <Select
                                    options={provinces?.map((province) => ({
                                        value: province.id,
                                        label: province.name,
                                    }))}
                                    onChange={() => form.setFieldValue('parentFederationId', undefined)}
                                />
                            </Form.Item>
                        ) : (
                            <Form.Item name="provinceId" hidden rules={[{ required: true }]}>
                                <Input />
                            </Form.Item>
                        )}
                    </>
                )}

                {level === 'City' && (
                    <>
                        {auth.scopeLevel === 'Mastery' ? (
                            <Form.Item name="provinceId" label="استان" rules={[{ required: true }]}>
                                <Select
                                    options={provinces?.map((province) => ({
                                        value: province.id,
                                        label: province.name,
                                    }))}
                                    onChange={() => {
                                        form.setFieldsValue({
                                            parentFederationId: undefined,
                                            cityId: undefined,
                                        });
                                    }}
                                />
                            </Form.Item>
                        ) : (
                            <Form.Item name="provinceId" hidden rules={[{ required: true }]}>
                                <Input />
                            </Form.Item>
                        )}
                        <Form.Item name="parentFederationId" label="فدراسیون استانی" rules={[{ required: true }]}>
                            <Select
                                placeholder={!provinceId ? 'ابتدا استان را انتخاب کنید' : 'انتخاب رشته'}
                                disabled={!provinceId}
                                options={provincialFederations?.map((federation) => ({
                                    value: federation.id,
                                    label: federation.displayName,
                                }))}
                            />
                        </Form.Item>
                        {auth.scopeLevel === 'City' ? (
                            <Form.Item name="cityId" hidden rules={[{ required: true }]}>
                                <Input />
                            </Form.Item>
                        ) : (
                            <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                                <Select
                                    disabled={auth.scopeLevel === 'Mastery' && !provinceId}
                                    options={availableCities?.map((city) => ({
                                        value: city.id,
                                        label: auth.scopeLevel === 'Mastery'
                                            ? `${city.name} - ${city.provinceName}`
                                            : city.name,
                                    }))}
                                />
                            </Form.Item>
                        )}
                    </>
                )}
            </Form>
        </Modal>
    );
}
