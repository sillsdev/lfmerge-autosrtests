# Automated LfMerge S/R Tests

**NOTE:** This project requires Mono 4.x, i.e. the package `mono4-sil` has to be installed!

The mocked LanguageDepot/Chorus repo as well as the mongo database data are stored
in the source tree as series of patch files in the data directory. The Chorus repo
and the mongo database will be restored during a test run.

The data is stored as patch files rather than in binary format so that it is
possible to easily see the difference between two versions of the test data.

## Creating new tests - semi-automated way

### Restore existing Chorus repo

To restore a Chorus repo up to revision _rev_, run the following command. The patch
files (up to revision _rev_; in the example below this would be `r0.patch` and `r1.patch`) are taken from the
`data/7000068` directory and the Chorus repo will end up in
`/tmp/testdata/LanguageDepot`.

	mono --debug TestUtil.exe restore --ld=1 --workdir=/tmp/testdata/ \
		--project=autosrtests --model=7000068

### Export existing Chorus repo

Make the changes in the lowest supported version of FLEx (currently 8.2.9). Then
run send/receive to a USB stick. Afterwards copy the directory from the USB stick to
your hard drive (e.g. /tmp/ld) to speed up processing.

The new changes can be exported by running:

	mono --debug TestUtil.exe save --ld --workdir /tmp/ld --project autosrtests \
		--model 7000068

This will save the new patches in `data/7000068`.

**NOTE:** When doing a S/R in FLEx to the USB stick be aware that _flexbridge_ identifies
the correct directory by checking the hash of the first commit. If multiple repos
exist with the same history it is unclear which directory _flexbridge_
chooses, so you might get unexpected results.

### Restore test project in Mongo

The patches for the test project in the mongo database are located as patches in
`data/<modelversion>/mongo`. To restore version _2_ for model version
_7000068_ run the following command:

	mono --debug TestUtil.exe restore --mongo=2 --project autosrtests \
		--workdir /tmp/testdata --model 7000068

### Export test project in Mongo

Make the changes in _LanguageForge_, then run:

	mono --debug TestUtil.exe save --mongo --project autosrtests --msg "commit msg" \
		--workdir /tmp/testdata --model 7000068

### Merge Chorus and Mongo test data

Before writing a new test you'll have to created the merged data, i.e. get the data
in the state it would be after a send/receive. This is necessary because currently
the patches are consecutive (if necessary this could be changed by introducing an
additional path parameter).

The merged data based on mongo patch _2_ and
language depot patch _2_ for model version 7000068 can be created by running:

	mono --debug TestUtil.exe merge --mongo=2 --project autosrtests \
		--ld=2 --workdir=/tmp/testdata/ --model=7000068

### Steps to create a new test

To create a new test you'll need to make the necessary changes in FieldWorks
and LF, then export the changes and write the unit test.

#### Create changes in LanguageDepot/Chorus

1. Restore the Chorus repo:


	mono --debug TestUtil.exe restore --ld=3 --project autosrtests \
		--workdir /tmp/testdata --model 7000068

2. Copy the `.hg` subdirectory from `/tmp/testdata/LanguageDepot` to the USB stick


	cp -a /tmp/testdata/LanguageDepot/.hg /media/username/USBstick/test-7000068

3. In FW get the testproject from the USB stick. This might involve deleting the
   existing project in FW. If you re-use the existing project make sure that later
   on it doesn't create extra commits in the Chorus repo.

4. Make the desired changes in FW

5. S/R from FLEx to the USB stick

6. Copy the `test-7000068` folder from the USB stick to the `/tmp` directory

7. Export the new commit of the simulated LanguageDepot by running:


	mono --debug TestUtil.exe save --ld --workdir /tmp/test-7000068 \
		--project autosrtests --model 7000068

#### Create changes in LanguageForge

1. Restore project in Mongo


	mono --debug TestUtil.exe restore --mongo=3 --project autosrtests \
		--workdir /tmp/testdata --model 7000068

2. Make changes in local LF

3. Export Mongo test data


	mono --debug TestUtil.exe save --mongo --project autosrtests \
		--msg "commit msg" --workdir /tmp/testdata --model 7000068

#### Changes in `LfMerge.AutomatedSRTests`

- create the new unit test
- repeat the above steps for each model version
- merge the Chorus and Mongo test data for each model version:


		mono --debug TestUtil.exe merge --mongo=2 --project autosrtests \
			--ld=2 --workdir=/tmp/testdata/ --model=7000068

## Creating new tests - the hard way

### Export existing Chorus repo

	mkdir -p $modelversion
	for i in $(hg log -b $modelversion --template "{rev} "); do
		hg export -r $i > $modelversion/r$i.patch
	done

### Restore existing Chorus repo

The mocked LanguageDepot/Chorus repo is stored in the source tree as a series of
patch files in the `data` directory. The repo will be restored during a test run.
However, for adding or modifying tests it might be necessary to manually restore the
repo. This can be achieved by creating a Mercurial repo and then applying the patches:

	mkdir -p workdir/testproj
	hg init .
	hg import ../../data/r0.patch
	hg import ../../data/7000068/r1.patch
	etc.

### Export Mongo database

	mongoexport --db scriptureforge --collection projects --query '{ "projectName" : $projectname}' > $projectname.json
	for col in activity lexicon optionlists; do
		mongoexport --db $projectname --collection $col > ${projectname}.${col}.json
	done

### Restore Mongo database

	mongoimport --db scriptureforge --collection projects --file $dbname.json
	for col in activity lexicon optionlists; do
		mongoimport --db $dbname --drop --collection $col --file ${dbname}.${col}.json
	done
