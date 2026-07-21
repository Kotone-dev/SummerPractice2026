import json
with open('D:/Practice2026/kanban_check.json', 'r', encoding='utf-8-sig') as f:
    data = json.loads(f.read())
for item in data['items']:
    if item['title'].startswith('7.'):
        print(f"{item['title']}: {item['status']}")
