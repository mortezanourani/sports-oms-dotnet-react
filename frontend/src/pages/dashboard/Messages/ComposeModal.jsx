import { useState } from 'react';
import { Modal, Form, Input, Select, Upload, Button, message } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { useSendMessage, useAddAttachment, useRecipientOptions } from '../../../hooks/useMessages';
import { ROLE_LABELS, SCOPE_LEVEL_LABELS } from '../../../constants/labels';
import { useAuth } from '../../../hooks/useAuth';

const { TextArea } = Input;

export default function ComposeModal({ open, onClose }) {
    const [form] = Form.useForm();
    const [fileList, setFileList] = useState([]);
    const [sending, setSending] = useState(false);
    const { role, scopeLevel } = useAuth();
    const isFederationRep = role === 'FederationRep';
    const recipientType = Form.useWatch('recipientType', form);
    const recipientScopeLevel = Form.useWatch('scopeLevel', form);
    const peerRoles = [
        'Manager', 'Admin', 'SportsManager', 'M5Manager',
        'M88Manager', 'HumanResourceManager', 'InsuranceOfficer',
    ];
    const recipientScopeOptions = (() => {
        if (scopeLevel === 'Mastery') {
            return ['Mastery', ...(peerRoles.includes(role) ? ['Province'] : [])];
        }
        if (scopeLevel === 'Province') {
            return [
                ...(peerRoles.includes(role) ? ['Mastery'] : []),
                'Province',
                'City',
            ];
        }
        if (scopeLevel === 'City') {
            return [...(peerRoles.includes(role) ? ['Province'] : []), 'City'];
        }
        return [];
    })();

    const recipientParams = {
        recipientType,
        ...(recipientType === 'MainRole' && !isFederationRep
            ? { scopeLevel: recipientScopeLevel }
            : {}),
    };
    const hasRecipientScope = recipientType === 'FederationRep' ||
        (recipientType === 'MainRole' && (isFederationRep || !!recipientScopeLevel));
    const canReceiveFederationMessages =
        isFederationRep || ['Admin', 'Manager', 'SportsManager'].includes(role);
    const { data: recipients, isLoading: recipientsLoading } = useRecipientOptions(
        recipientParams,
        !!recipientType && hasRecipientScope,
    );
    const sendMutation = useSendMessage();
    const attachMutation = useAddAttachment();

    const handleFinish = async (values) => {
        setSending(true);
        try {
            // Step 1: create the message itself with no attachments — the
            // backend's design requires a message to already exist (with a
            // real Id) before an attachment row can reference it via
            // MessageId, so this can't be a single combined request.
            const { id: messageId } = await sendMutation.mutateAsync(values);

            // Step 2: upload each selected file against that new message's Id,
            // one request per file. Uploads run sequentially (awaited in a
            // loop, not Promise.all) so a failure partway through leaves a
            // clear, predictable state — "message sent, first 2 of 3 files
            // attached" — rather than files landing in a random, hard-to-reason
            // about order under concurrent requests.
            for (const fileWrapper of fileList) {
                await attachMutation.mutateAsync({ messageId, file: fileWrapper.originFileObj });
            }

            message.success('پیام با موفقیت ارسال شد');
            form.resetFields();
            setFileList([]);
            onClose();
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در ارسال پیام');
        } finally {
            setSending(false);
        }
    };

    return (
        <Modal
            title="پیام جدید"
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            confirmLoading={sending}
            okText="ارسال"
            cancelText="انصراف"
            destroyOnClose
        >
            <Form form={form} layout="vertical" onFinish={handleFinish}>
                <Form.Item
                    name="recipientType"
                    label="نوع گیرنده"
                    rules={[{ required: true, message: 'نوع گیرنده را انتخاب کنید' }]}
                >
                    <Select
                        placeholder="انتخاب نوع گیرنده"
                        onChange={() => form.setFieldsValue({ scopeLevel: undefined, recipientIds: undefined })}
                        options={[
                            { value: 'MainRole', label: 'کاربر اداری' },
                            ...(canReceiveFederationMessages
                                ? [{ value: 'FederationRep', label: 'فدراسیون/هیات' }]
                                : []),
                        ]}
                    />
                </Form.Item>
                {recipientType === 'MainRole' && !isFederationRep && (
                    <Form.Item
                        name="scopeLevel"
                        label="حوزه گیرنده"
                        rules={[{ required: true, message: 'حوزه گیرنده را انتخاب کنید' }]}
                    >
                        <Select
                            placeholder="انتخاب حوزه"
                            onChange={() => form.setFieldValue('recipientIds', undefined)}
                            options={recipientScopeOptions.map((value) => ({
                                value,
                                label: SCOPE_LEVEL_LABELS[value],
                            }))}
                        />
                    </Form.Item>
                )}
                <Form.Item
                    name="recipientIds" label="گیرندگان"
                    rules={[{ required: true, message: 'انتخاب حداقل یک گیرنده الزامی است' }]}
                >
                    <Select
                        mode="multiple"
                        disabled={!recipientType || !hasRecipientScope}
                        loading={recipientsLoading}
                        placeholder="انتخاب گیرندگان"
                        options={recipients?.map((r) => ({
                            value: r.id,
                            label: r.role === 'FederationRep' && r.federationPositions.length
                                ? `${r.fullName} (${r.federationPositions.join('، ')})`
                                : `${r.fullName} (${[
                                    ROLE_LABELS[r.role] || r.role,
                                    SCOPE_LEVEL_LABELS[r.scopeLevel] || r.scopeLevel,
                                    r.provinceName || r.cityName,
                                ].filter(Boolean).join(' ')})`,
                        }))}
                        optionFilterProp="label"
                    />
                </Form.Item>
                <Form.Item name="subject" label="موضوع">
                    <Input />
                </Form.Item>
                <Form.Item name="body" label="متن پیام" rules={[{ required: true }]}>
                    <TextArea rows={5} />
                </Form.Item>
                <Form.Item label="پیوست‌ها">
                    <Upload
                        fileList={fileList}
                        beforeUpload={() => false} // prevents Ant Design's automatic upload — files stay queued until Step 2 above runs them manually with the real messageId
                        onChange={({ fileList: newList }) => setFileList(newList)}
                        multiple
                    >
                        <Button icon={<UploadOutlined />}>انتخاب فایل</Button>
                    </Upload>
                </Form.Item>
            </Form>
        </Modal>
    );
}
