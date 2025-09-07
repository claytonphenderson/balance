# Use this if you need to set the balance to a particular amount
DOCUMENT='{ "date": new Date("2025-09-06T15:59:02.201+00:00"), "meta": {}, "merchant": "FAKE", "total": 00.00 }'

mongosh --quiet <<EOF
use balance
db.expenses.insertOne($DOCUMENT)
quit()
EOF