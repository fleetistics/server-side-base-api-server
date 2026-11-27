-- The English wording of the "contact-message" email (the data it can use is ContactMessageData in api-server/Services/Notifications).
-- Fluid (Liquid) text. The subject line is typed by the sender (or "Message from <name>"); the body is plain text, escaped in the HTML.
-- Safe to re-run: nothing happens when the key already has an English version. The wording can be changed on Portal Admin > Message templates.
INSERT INTO notification_template ("TemplateKey", "Language", "Version", "IsActive", "Subject", "Text", "Html", "CreatedAt", "CreatedBy")
SELECT 'contact-message', 'en', 1, TRUE,
       '{{ Subject }}',
       'Hi {{ RecipientName }},

{{ Body }}

--
{{ SenderName }}, {{ SenderCompanyName }}
You can reply to this email: the answer goes to {{ SenderName }}.',
       '<p>Hi {{ RecipientName }},</p>
<div style="white-space:pre-wrap">{{ Body }}</div>
<p style="color:#666;font-size:12px">{{ SenderName }}, {{ SenderCompanyName }}<br>You can reply to this email: the answer goes to {{ SenderName }}.</p>',
       now(), 'seed'
WHERE NOT EXISTS (
    SELECT 1 FROM notification_template t WHERE t."TemplateKey" = 'contact-message' AND t."Language" = 'en'
);
