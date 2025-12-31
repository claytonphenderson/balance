## run this after you pay the bill
mongosh "mongodb://localhost:27018" --quiet <<EOF
use balance
db.expenses.deleteMany({})
quit()
EOF