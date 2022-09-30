using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transform.Test.Properties;

namespace Transform.Test.Tests.TestRequestFGIS
{
    public class RequestFGIS
    {
        [Test]
        public void TestOKPD2Type()
        {
            execTestTransfornXml(Resources.OKPD2TypeRequest);
        }

        [Test]
        public void TestRequestCanceledExtinctionRefusal()
        {
            execTestTransfornXml(Resources.RequestCanceledExtinctionRefusal);
        }

        [Test]
        public void TestRequestCanceledLot()
        {
            execTestTransfornXml(Resources.RequestCanceledLot);
        }

        [Test]
        public void TestRequestCanceledSDIZ()
        {
            execTestTransfornXml(Resources.RequestCanceledSDIZ);
        }

        [Test]
        public void TestRequestCreateExtinctionRefusal()
        {
            execTestTransfornXml(Resources.RequestCreateExtinctionRefusal);
        }

        [Test]
        public void TestRequestCreateExtinction()
        {
            execTestTransfornXml(Resources.RequestCreateExtinction);
        }

        [Test]
        public void TestRequestCreateLot()
        {
            execTestTransfornXml(Resources.RequestCreateLot);
        }

        [Test]
        public void TestRequestCreateLotDebit()
        {
            execTestTransfornXml(Resources.RequestCreateLotDebit);
        }

        [Test]
        public void TestRequestCreateLotOnElevator()
        {
            execTestTransfornXml(Resources.RequestCreateLotOnElevator);
        }

        [Test]
        public void TestRequestCreateSDIZ()
        {
            execTestTransfornXml(Resources.RequestCreateSDIZ);
        }

        [Test]
        public void TestRequestCreateSDIZElevator()
        {
            execTestTransfornXml(Resources.RequestCreateSDIZElevator);
        }

        [Test]
        public void TestRequestGetListExtinction()
        {
            execTestTransfornXml(Resources.RequestGetListExtinction);
        }

        [Test]
        public void TestRequestGetListExtinctionRefusal()
        {
            execTestTransfornXml(Resources.RequestGetListExtinctionRefusal);
        }

        [Test]
        public void TestRequestGetListLot()
        {
            execTestTransfornXml(Resources.RequestGetListLot);
        }

        [Test]
        public void TestRequestGetListLotDebit()
        {
            execTestTransfornXml(Resources.RequestGetListLotDebit);
        }

        [Test]
        public void TestRequestGetListSDIZ()
        {
            execTestTransfornXml(Resources.RequestGetListSDIZ);
        }

        [Test]
        public void TestRequestGetListSDIZElevator()
        {
            execTestTransfornXml(Resources.RequestGetListSDIZElevator);
        }

        public static void execTestTransfornXml(string xml)
        {
            TestFromMetodichka.SmevTransformSpiTests.execTestTransfornXml(xml);
        }
    }
}
