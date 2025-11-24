# Form Creation

Forms must be created following a specific format or the responses will not be properly recorded by the system. Follow
the steps below to ensure that your form is parsed correctly.

1. Title
    1. Your choice. Recommend "{Institution Name} 3D Print Submission Form"
2. Questions
    1. Name
        1. Create a "Short answer" question.
        2. The question text must contain "Name". Capitalization does matter.
        3. No other question may contain "Name" in the question text.
        4. Recommend making this required.
        5. Recommend making the question text "Name"
        6. Optionally, add a description to provide further context or instructions.
    2. Discord Username
        1. Create a "Short answer" question.
        2. The question text must contain "Discord". Capitalization does matter.
        3. No other question may contain "Discord" in the question text.
        4. Recommend _not_ making this required.
        5. Recommend making the question text "Discord Username (Optional)"
        6. Recommend adding a description "This will be used to send you updates on your print submission. If you have
           submitted this information previously, you do not need to do so again."
    3. .gcode File
        1. Create a "File upload" question.
        2. The question text must contain "File". Capitalization does matter.
        3. No other question may contain "File" in the question text.
        4. Maximum number of files set to 1
        5. Recommend maximum file size set to 100MB or 1GB
        6. Make this question required.
        7. Optionally, add a description to provide further context or instructions.
        8. Recommend description at least "You must upload a single .gcode file."
    4. Number of copies
        1. Create a "Linear scale" question.
        2. The question text must contain "copies". Capitalization does matter.
        3. No other question may contain "copies" in the question text.
        4. Set the scale to be 1 to 10 (or the max number of copies you want to allow)
        5. Make this question required.
        6. Recommend adding a description "How many copies of the uploaded .gcode file will be printed. You will be
           charged for each copy."
3. Form settings:
    1. Responses:
        1. Collect email addresses: Verified
        2. Send responders a copy of their response: Recommend Off, anything is fine
        3. Allow response editing: Off
        4. Limit to 1 response: Off
        5. Total size limit for all uploaded files: Recommend 100GB or 1TB
    2. Presentation
        1. Show progress bar: Recommend off
        2. Shuffle question order: Off
        3. Confirmation message: Recommend default
        4. Show link to submit another response: Recommend On
        5. View results summary: Off
        6. Disable autosave for all respondents: Off
4. Adding form to environment variables
    1. Once the form has been created, the form ID needs to be added to the .env file. This can be found in the URL of
       the edit page of the form.
    2. Ex) https://docs.google.com/forms/d/FORM_ID_IS_HERE/edit
    3. GOOGLE_FORM_ID=FORM_ID_IS_HERE
5. Google service account
    1. A service account must be created and given access to the google form.
    2. Go to https://console.cloud.google.com and login with the google account you use to manage the form.
    3. In the top left, select the "Project picker" (or press ctrl+o)
    4. In the top right of the modal that opens, click "New project"
    5. Give the project a name such as {Institution Name} - OpenFarm
    6. Open the project picker and select the newly created project once it is ready.
    7. Go to "IAM & Admin" under Quick Access or press "." to open the sidebar and select it there
    8. Go to "Service Accounts" in the left sidebar
    9. Near the top, click "+ Create service account"
    10. Give the account a name such as "OpenFarm Service Account" and click Create and continue
    11. Add the "Owner" role under permissions
    12. On the "Service Accounts" tab, select the account you created.
    13. On the "Keys" tab, click "Add key" -> Create new key.
    14. Select JSON and click "Create". A key will be automatically downloaded.
    15. Put the .json file downloaded into the google-form-listener folder.
    16. Add "GOOGLE_SERVICE_ACCOUNT_CRED_FILE=FILE_NAME_HERE.json\" to the .env file with the appropriate name. You may
        rename the file if you wish.
6. Sharing the form
    1. The google service account must be given access to the form.
    2. On the form edit page, go to the share settings in the top right of the page
    3. Add the google form service account email as an editor. This can be found in the secret.json file you downloaded
       in step 5.xiv under "client_email"
    4. Recommend Responder view is restricted, and you add all allowed submitters manually or if the form is created by
       an account within your organization you can restrict access to only that organization.
