-- The English wording of the "join-invitation" email (the data it can use is JoinInvitationData in api-server/Services/Notifications).
-- Fluid (Liquid) text. The link carries the code too (/join?code=...). The code is wrapped in <secure>...</secure> so the email sender keeps it out of what it stores.
-- Safe to re-run: nothing happens when the key already has an English version. The wording can be changed on Portal Admin > Message templates.
INSERT INTO notification_template ("TemplateKey", "Language", "Version", "IsActive", "Subject", "Text", "Html", "CreatedAt", "CreatedBy")
SELECT 'join-invitation', 'en', 1, TRUE,
       '{{ InviterName }} invited you to join {{ CompanyName }}',
       'Hi {{ InviteeName }},

{{ InviterName }} invited you to join {{ CompanyName }}.
{% if Message %}
"{{ Message }}"
{% endif %}
To accept, open this page and sign in (the code is filled in for you):
{{ AcceptLink }}

If you are asked for it, the code is:
<secure>{{ JoinCode }}</secure>

The invitation is open until {{ ExpiresAtUtc | date: "%Y-%m-%d %H:%M" }} UTC. If you did not expect it, ignore this email.',
       '<p>Hi {{ InviteeName }},</p>
<p><strong>{{ InviterName }}</strong> invited you to join <strong>{{ CompanyName }}</strong>.</p>
{% if Message %}<blockquote>{{ Message }}</blockquote>{% endif %}
<p>To accept, <a href="{{ AcceptLink }}">open the invitation page</a> and sign in. The code is filled in for you; if you are asked for it, it is:</p>
<p style="font-size:24px;font-weight:bold;letter-spacing:3px;font-family:monospace"><secure>{{ JoinCode }}</secure></p>
<p>The invitation is open until {{ ExpiresAtUtc | date: "%Y-%m-%d %H:%M" }} UTC. If you did not expect it, ignore this email.</p>',
       now(), 'seed'
WHERE NOT EXISTS (
    SELECT 1 FROM notification_template t WHERE t."TemplateKey" = 'join-invitation' AND t."Language" = 'en'
);
