// Security rule tests for storage.rules (cover photos), against the Storage + Firestore emulators.
// Storage rules look up library membership in Firestore, so both emulators run.
import { readFileSync } from 'node:fs';
import { after, before, beforeEach, describe, it } from 'node:test';
import { assertFails, assertSucceeds, initializeTestEnvironment } from '@firebase/rules-unit-testing';
import { doc, setDoc } from 'firebase/firestore';
import { ref, uploadBytes, getBytes, deleteObject } from 'firebase/storage';

const CHRIS = { uid: 'chris', email: 'chris@example.com' };
const WIFE = { uid: 'wife', email: 'wife@example.com' };
const STRANGER = { uid: 'stranger', email: 'stranger@example.com' };
const LIB = 'lib1';
const PHOTO = '0123456789abcdef0123456789abcdef';
const JPEG = new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0xff, 0xd9]);

let env;

const storageAs = user => env.authenticatedContext(user.uid, { email: user.email, email_verified: true }).storage();
const cover = (storage, name = `${PHOTO}.jpg`, lib = LIB) => ref(storage, `libraries/${lib}/covers/${name}`);
const upload = (storage, name, data = JPEG, contentType = 'image/jpeg') =>
    uploadBytes(cover(storage, name), data, { contentType });

before(async () => {
    env = await initializeTestEnvironment({
        projectId: 'demo-bookshelf',
        firestore: { rules: readFileSync(new URL('../../firestore.rules', import.meta.url), 'utf8') },
        storage: { rules: readFileSync(new URL('../../storage.rules', import.meta.url), 'utf8') },
    });
});

after(() => env.cleanup());

beforeEach(async () => {
    await env.clearFirestore();
    await env.clearStorage();
    await env.withSecurityRulesDisabled(async context => {
        await setDoc(doc(context.firestore(), 'libraries', LIB), {
            name: 'The Hill Library',
            ownerUid: CHRIS.uid,
            memberUids: [CHRIS.uid, WIFE.uid],
            invitedEmails: [],
            members: {},
        });
    });
});

describe('cover photos', () => {
    it('members can upload, read and delete', async () => {
        await assertSucceeds(upload(storageAs(CHRIS)));
        await assertSucceeds(getBytes(cover(storageAs(WIFE))));
        await assertSucceeds(deleteObject(cover(storageAs(WIFE))));
    });

    it('non-members and signed-out users cannot read or write', async () => {
        await upload(storageAs(CHRIS));
        await assertFails(getBytes(cover(storageAs(STRANGER))));
        await assertFails(upload(storageAs(STRANGER), 'abcdefgh12345678.jpg'));
        await assertFails(deleteObject(cover(storageAs(STRANGER))));
        await assertFails(getBytes(cover(env.unauthenticatedContext().storage())));
    });

    it('rejects non-JPEGs, odd names and other libraries', async () => {
        const storage = storageAs(CHRIS);
        await assertFails(upload(storage, `${PHOTO}.jpg`, JPEG, 'image/png'));
        await assertFails(upload(storage, '../../evil.jpg'));
        await assertFails(upload(storage, 'short.jpg'));
        await assertFails(upload(storage, `${PHOTO}.exe`));
        await assertFails(uploadBytes(cover(storage, `${PHOTO}.jpg`, 'someone-elses-library'), JPEG, { contentType: 'image/jpeg' }));
    });

    it('rejects files of 2 MB or more', async () => {
        await assertFails(upload(storageAs(CHRIS), `${PHOTO}.jpg`, new Uint8Array(2 * 1024 * 1024)));
    });

    it('nothing outside library covers is writable', async () => {
        await assertFails(uploadBytes(ref(storageAs(CHRIS), 'anything.jpg'), JPEG, { contentType: 'image/jpeg' }));
    });
});
