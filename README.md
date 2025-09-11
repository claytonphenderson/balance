# balance
A very basic budget __awareness__ tool.  Sends you an email when your credit card is used telling you the current balance and how much of your budget you have spent.

![Example](./src/WorkerHost/example.jpeg)

### How to run
Add the following environment variables or update the appsettings.json file with your info.
```
  "Database":{
    "MongoUrl": "mongodb://localhost:27017/"
  },
    "EmailAppPassword": "",
    "EmailTo": [],
    "EmailFrom": "",
    "SpendLimit": ""
```

then 
```
dotnet run
```

### Tools
At this point, theres not an automated way to reset the running balance other than to run `tools/reset-balance.sh` which just deletes all the entries in the mongo collection.  Eventually I'll probably add something to handle this better, but for now I'll just run that every time I pay the bill.

You can also run `tools/set-balance.sh` to put a dummy transaction into the collection so you can correct the current balance if you need to.

### Todo
- automate resetting the balance every month

### Rant
All I want is an easy way to know what my credit card balance is relative to my budget for the month.  The Chase app is fine, but I have to log in to use that.  Even with FaceID, its enough of a hassle that I just don't check as frequently as I would like to.  Other "budgeting" and personal finance apps are truly terrible.  Aside from the pain and privacy concerns of linking your financial institutions, you have to deal with shitty UIs, way too many notifications, ads, and then at the end of the day its usually still not exactly what you want.

Okay so I decided I would make a super basic tool to help me with this.  The plan is to hook into something that would let me know when my card was used, inform me of my current balance, and let me know how much of my budget has been used so far this month.  Should be super easy but damn everything is locked down these days.

First, getting the card balance is way harder than it should be.  Plaid is a great tool, but I don't plan on making this a multi-tenant tool and I really don't want to go through the regulatory BS that plaid asks of you when you sign up for an account. Plus, the Chase + Plaid integration requires an Oauth flow that I also don't want to mess with seeing as this is a server-side application. Okay, how about Chase then? Surely they have basic APIs available?  They do, but they also appear to be much more geared toward large businesses rather than individuals. I didn't spend too much time looking there as their offerings didn't seem to match what I was looking for.  

Finally, I landed on... scraping emails.  Turns out Chase does offer email notifications that your card was run, for what amount, and with which merchant.  No balance included, but this is enough to go on.  So I generated an app password for my gmail account, and Im using IMAP to check my inbox periodically for the chase notifications.  When I receive a notification, I strip out the details and save them as a document in a mongo collection.  

Great, so once Ive done all that, I just need to sum up all the transactions and theres my balance.  I configure the app with a budget amount so it just should be able to tell me that Im at X% of my budget.  Now I just need to set up a SMS integration to send me this info when Ive run my card.

Holy shit has programmatic SMS gone to hell.  First I tried Azure's communication services SMS feature - of course everything is geared toward a business trying to send promotional materials.  So they effectively make you jump through regulatory hoops to make sure you dont illegally spam people.  I dont want to deal with this so I go check out Twilio to find its the same shit show over there.  No options for a developer to build their own integration just to send a few messages a day to numbers that they verify as their own.

After this I looked into Signal's developer support.  A quick search didnt show much in terms of an official api, but I did find a nice CLI tool (https://github.com/AsamK/signal-cli) that I figured was the best option I had.  I got that all set up just to realize that sending messages to yourself in signal does not trigger a notification... So that idea was scrapped.  This left me with not much choice other than email, so I decided to just use SMTP to send myself an email. Not ideal, but better than applying for regulatory approval for a weekend project.

So the final result is that I have an application that runs on my home pc that checks my email for chase spending notifications, records them in a mongo collection locally, and emails me a summary that lets me know that I am X% through my monthly budget. I specifically put a 🔴,🟡, or 🟢 budget indicator in the subject so that will show up on my lock screen and I will know my budget status without opening or logging into anything.